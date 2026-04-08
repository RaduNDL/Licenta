using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Licenta.Pages.Patient.Messages;

[Authorize(Roles = "Patient")]
public class InboxModel : PageModel                                                                 
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly INotificationService _notifications;

    private const string ConversationVisibilityEntity = "PatientConversation";
    private const string HideConversationAction = "HideConversationPatient";
    private const string RevealConversationAction = "RevealConversationPatient";
    private const int PreviewMaxLength = 50;

    public InboxModel(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IHubContext<NotificationHub> hub,
        INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
        _notifications = notifications;
    }

    [BindProperty(SupportsGet = true)]
    public Guid requestId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? kind { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool showHidden { get; set; }

    public Guid SelectedRequestId { get; set; }
    public string SelectedKind { get; set; } = "";
    public string? SelectedPartnerId { get; set; }
    public string SelectedUserName { get; set; } = "";
    public string SelectedSubtitle { get; set; } = "";
    public string CurrentConversationKey { get; set; } = "";
    public bool CanSend { get; set; }
    public bool ShowHidden { get; set; }

    public List<ConversationVm> Conversations { get; set; } = new();
    public List<MessageVm> Messages { get; set; } = new();

    public record ConversationVm(
        Guid RequestId,
        string Kind,
        string? PartnerId,
        string PartnerName,
        string LastMessagePreview,
        DateTime LastAt,
        string ConversationKey,
        bool IsClosed,
        bool IsHidden);

    public record MessageVm(
        Guid Id,
        bool IsMine,
        string Body,
        DateTime SentAtUtc,
        DateTime SentAtLocal);

    public class InputModel
    {
        public Guid RequestId { get; set; }
        public string Kind { get; set; } = "";
        public string? PartnerId { get; set; }
        public string NewMessageBody { get; set; } = "";
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    private static string ThreadKey(Guid reqId) => $"REQ:{reqId}";

    private static string ConversationKey(Guid reqId, string kindNorm)
        => $"req:{reqId}:{kindNorm.ToLowerInvariant()}";

    private static string NormalizeKind(string? k)
        => (k ?? "Assistant").Trim().ToLowerInvariant() switch
        {
            "doctor" => "Doctor",
            _ => "Assistant"
        };

    private static string TruncatePreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text.Length > PreviewMaxLength
            ? text[..PreviewMaxLength] + "..."
            : text;
    }

    private bool IsAjaxRequest()
        => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private Task<ApplicationUser?> CurrentUserAsync()
        => _userManager.GetUserAsync(User);

    private async Task<Guid?> GetPatientProfileIdAsync(string userId)
        => await _db.Patients
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

    private async Task<string?> ResolveDoctorUserIdAsync(Guid doctorProfileId)
        => await _db.Doctors
            .Where(d => d.Id == doctorProfileId)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync();

    private async Task<HashSet<string>> GetHiddenConversationKeysAsync(string userId)
    {
        var logs = await _db.UserActivityLogs
            .AsNoTracking()
            .Where(l =>
                l.UserId == userId &&
                l.EntityName == ConversationVisibilityEntity &&
                (l.Action == HideConversationAction || l.Action == RevealConversationAction) &&
                !string.IsNullOrWhiteSpace(l.EntityId))
            .Select(l => new
            {
                l.EntityId,
                l.Action,
                l.OccurredAtUtc
            })
            .ToListAsync();

        return logs
            .GroupBy(x => x.EntityId!)
            .Select(g => g.OrderByDescending(x => x.OccurredAtUtc).First())
            .Where(x => x.Action == HideConversationAction)
            .Select(x => x.EntityId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task WriteConversationVisibilityAsync(string userId, Guid reqId, string kindNorm, string action)
    {
        _db.UserActivityLogs.Add(new UserActivityLog
        {
            UserId = userId,
            Action = action,
            EntityName = ConversationVisibilityEntity,
            EntityId = ConversationKey(reqId, kindNorm),
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new
            {
                requestId = reqId,
                kind = kindNorm,
                hidden = action == HideConversationAction
            })
        });

        await _db.SaveChangesAsync();
    }

    private async Task LoadConversationsAsync(
        ApplicationUser patient,
        Guid patientProfileId,
        HashSet<string> hiddenConversationKeys,
        bool includeHidden)
    {
        Conversations.Clear();

        var requests = await _db.PatientMessageRequests
            .AsNoTracking()
            .Where(r =>
                r.PatientId == patientProfileId &&
                (r.Status == PatientMessageRequestStatus.ActiveDoctorChat ||
                 r.Status == PatientMessageRequestStatus.AssistantChat ||
                 r.Status == PatientMessageRequestStatus.Pending ||
                 r.Status == PatientMessageRequestStatus.Closed))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        if (requests.Any())
        {
            var requestIds = requests.Select(r => $"REQ:{r.Id}").ToList();
            var lastMessages = await _db.InternalMessages
                .AsNoTracking()
                .Where(m => requestIds.Contains(m.Subject))
                .GroupBy(m => m.Subject)
                .Select(g => new
                {
                    Subject = g.Key,
                    LastMsg = g.OrderByDescending(x => x.SentAt).FirstOrDefault()
                })
                .ToListAsync();

            var lastMessageDict = lastMessages.ToDictionary(x => x.Subject, x => x.LastMsg);

            foreach (var r in requests)
            {
                if (r.Status == PatientMessageRequestStatus.ActiveDoctorChat ||
                    r.Status == PatientMessageRequestStatus.Closed)
                {
                    var doctorUserId = await ResolveDoctorUserIdAsync(r.DoctorProfileId);
                    if (!string.IsNullOrWhiteSpace(doctorUserId))
                    {
                        var threadKey = ThreadKey(r.Id);
                        var lastMsg = lastMessageDict.TryGetValue(threadKey, out var msg) ? msg : null;
                        var doctorConversation = new ConversationVm(
                            r.Id,
                            "Doctor",
                            doctorUserId,
                            (await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == doctorUserId))?.FullName
                                ?? (await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == doctorUserId))?.Email
                                ?? "Doctor",
                            TruncatePreview(lastMsg?.Body ?? r.Subject),
                            lastMsg?.SentAt ?? (r.UpdatedAt ?? r.CreatedAt),
                            ConversationKey(r.Id, "Doctor"),
                            r.Status == PatientMessageRequestStatus.Closed,
                            hiddenConversationKeys.Contains(ConversationKey(r.Id, "Doctor")));

                        if (doctorConversation.IsHidden == includeHidden)
                        {
                            Conversations.Add(doctorConversation);
                        }
                    }
                }

                if ((r.Status == PatientMessageRequestStatus.Pending ||
                     r.Status == PatientMessageRequestStatus.AssistantChat ||
                     r.Status == PatientMessageRequestStatus.Closed) &&
                    !string.IsNullOrWhiteSpace(r.AssistantId))
                {
                    var threadKey = ThreadKey(r.Id);
                    var lastMsg = lastMessageDict.TryGetValue(threadKey, out var msg) ? msg : null;
                    var assistantUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == r.AssistantId);
                    var assistantConversation = new ConversationVm(
                        r.Id,
                        "Assistant",
                        r.AssistantId,
                        assistantUser?.FullName ?? assistantUser?.Email ?? "Assistant",
                        TruncatePreview(lastMsg?.Body ?? r.Subject),
                        lastMsg?.SentAt ?? (r.UpdatedAt ?? r.CreatedAt),
                        ConversationKey(r.Id, "Assistant"),
                        r.Status == PatientMessageRequestStatus.Closed,
                        hiddenConversationKeys.Contains(ConversationKey(r.Id, "Assistant")));

                    if (assistantConversation.IsHidden == includeHidden)
                    {
                        Conversations.Add(assistantConversation);
                    }
                }
            }
        }

        Conversations = Conversations
            .OrderByDescending(c => c.LastAt)
            .ThenBy(c => c.PartnerName)
            .ToList();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var patient = await CurrentUserAsync();
        if (patient == null) return Unauthorized();

        ShowHidden = showHidden;

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null) return Page();

        var hiddenConversationKeys = await GetHiddenConversationKeysAsync(patient.Id);
        await LoadConversationsAsync(patient, patientProfileId.Value, hiddenConversationKeys, ShowHidden);

        if (requestId == Guid.Empty)
            return Page();

        SelectedRequestId = requestId;
        SelectedKind = NormalizeKind(kind);

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.PatientId == patientProfileId.Value);

        if (req == null)
            return NotFound();

        if (SelectedKind == "Doctor" &&
            (req.Status == PatientMessageRequestStatus.Pending ||
             req.Status == PatientMessageRequestStatus.AssistantChat))
        {
            TempData["StatusMessage"] = "This conversation has been delegated to an assistant.";
            return RedirectToPage(new { requestId = req.Id, kind = "Assistant" });
        }

        string? resolvedPartnerId = SelectedKind == "Doctor"
            ? await ResolveDoctorUserIdAsync(req.DoctorProfileId)
            : req.AssistantId;

        if (string.IsNullOrWhiteSpace(resolvedPartnerId))
        {
            CanSend = false;
            return Page();
        }

        SelectedPartnerId = resolvedPartnerId;

        var partner = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == resolvedPartnerId);

        SelectedUserName = partner?.FullName ?? partner?.Email ?? SelectedKind;

        var baseSubtitle = SelectedKind == "Assistant" && req.Status == PatientMessageRequestStatus.Pending
            ? "Assistant (waiting to join)"
            : SelectedKind;

        SelectedSubtitle = req.Status == PatientMessageRequestStatus.Closed
            ? $"{baseSubtitle} • Closed"
            : baseSubtitle;

        CurrentConversationKey = ConversationKey(req.Id, SelectedKind);

        CanSend = SelectedKind == "Doctor"
            ? req.Status == PatientMessageRequestStatus.ActiveDoctorChat
            : req.Status == PatientMessageRequestStatus.AssistantChat;

        Messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == ThreadKey(req.Id) &&
                ((m.SenderId == patient.Id && m.RecipientId == resolvedPartnerId) ||
                 (m.SenderId == resolvedPartnerId && m.RecipientId == patient.Id)))
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageVm(
                m.Id,
                m.SenderId == patient.Id,
                m.Body,
                m.SentAt,
                m.SentAt.ToLocalTime()))
            .ToListAsync();

        await _db.InternalMessages
            .Where(m =>
                m.Subject == ThreadKey(req.Id) &&
                m.RecipientId == patient.Id &&
                !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        Input.RequestId = req.Id;
        Input.Kind = SelectedKind;
        Input.PartnerId = resolvedPartnerId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var patient = await CurrentUserAsync();
        var body = Input.NewMessageBody?.Trim() ?? "";

        if (patient == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Unauthorized." });
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Message cannot be empty." });
            TempData["StatusMessage"] = "Message cannot be empty.";
            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = Input.RequestId, kind = NormalizeKind(Input.Kind) });
        }

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Patient profile not found." });
            TempData["StatusMessage"] = "Patient profile not found.";
            return RedirectToPage("/Patient/Messages/Inbox");
        }

        var req = await _db.PatientMessageRequests
            .FirstOrDefaultAsync(r => r.Id == Input.RequestId && r.PatientId == patientProfileId.Value);

        if (req == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Conversation not found." });
            TempData["StatusMessage"] = "Conversation not found.";
            return RedirectToPage("/Patient/Messages/Inbox");
        }

        var kindNorm = NormalizeKind(Input.Kind);

        if (kindNorm == "Doctor" && req.Status != PatientMessageRequestStatus.ActiveDoctorChat)
        {
            var error = (req.Status == PatientMessageRequestStatus.Pending || req.Status == PatientMessageRequestStatus.AssistantChat)
                ? "This conversation has been delegated to an assistant."
                : "Conversation is not open.";

            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error });
            TempData["StatusMessage"] = error;
            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = "Assistant" });
        }

        if (kindNorm == "Assistant" && req.Status != PatientMessageRequestStatus.AssistantChat)
        {
            var error = req.Status == PatientMessageRequestStatus.Pending
                ? "Assistant chat is not active yet."
                : "Conversation is not open.";

            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error });
            TempData["StatusMessage"] = error;
            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = "Assistant" });
        }

        string? resolvedPartnerId = kindNorm == "Doctor"
            ? await ResolveDoctorUserIdAsync(req.DoctorProfileId)
            : req.AssistantId;

        if (string.IsNullOrWhiteSpace(resolvedPartnerId))
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Partner not found." });
            TempData["StatusMessage"] = "Partner not found.";
            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = kindNorm });
        }

        var message = new InternalMessage
        {
            Id = Guid.NewGuid(),
            SenderId = patient.Id,
            RecipientId = resolvedPartnerId,
            Subject = ThreadKey(req.Id),
            Body = body,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.InternalMessages.Add(message);
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var partnerUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == resolvedPartnerId);
        if (partnerUser != null)
        {
            var inboxUrl = kindNorm == "Doctor"
                ? $"/Doctor/Messages/Inbox?requestId={req.Id}"
                : $"/Assistant/Messages/Inbox?requestId={req.Id}";

            await _notifications.NotifyAsync(
                partnerUser,
                NotificationType.Message,
                "New Message",
                $"You received a new message from {patient.FullName ?? patient.Email}.",
                actionUrl: inboxUrl,
                actionText: "Reply",
                relatedEntity: "InternalMessage",
                relatedEntityId: message.Id.ToString()
            );
        }

        var convKey = ConversationKey(req.Id, kindNorm);

        var payload = new
        {
            conversationKey = convKey,
            id = message.Id,
            requestId = req.Id,
            senderId = message.SenderId,
            kind = kindNorm,
            body = message.Body,
            sentAtUtc = message.SentAt,
            sentAtLocal = message.SentAt.ToLocalTime()
        };

        await _hub.Clients.Users(new[] { patient.Id, resolvedPartnerId })
            .SendAsync("message:new", payload);

        if (!IsAjaxRequest())
            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = kindNorm });

        return new JsonResult(new
        {
            ok = true,
            id = message.Id,
            body = message.Body,
            senderId = message.SenderId,
            sentAtUtc = message.SentAt,
            sentAtLocal = message.SentAt.ToLocalTime()
        });
    }

    public async Task<IActionResult> OnPostHideConversationAsync(Guid requestId, string kind)
    {
        var patient = await CurrentUserAsync();
        if (patient == null) return Unauthorized();

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null)
            return RedirectToPage("/Patient/Messages/Inbox");

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.PatientId == patientProfileId.Value);

        if (req == null)
        {
            TempData["StatusMessage"] = "Conversation not found.";
            return RedirectToPage("/Patient/Messages/Inbox");
        }

        if (req.Status != PatientMessageRequestStatus.Closed)
        {
            TempData["StatusMessage"] = "Only closed conversations can be hidden.";
            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = NormalizeKind(kind) });
        }

        var kindNorm = NormalizeKind(kind);

        await WriteConversationVisibilityAsync(patient.Id, req.Id, kindNorm, HideConversationAction);

        TempData["StatusMessage"] = "Conversation hidden. Open Hidden to reveal it later.";
        return RedirectToPage("/Patient/Messages/Inbox");
    }

    public async Task<IActionResult> OnPostRevealConversationAsync(Guid requestId, string kind)
    {
        var patient = await CurrentUserAsync();
        if (patient == null) return Unauthorized();

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null)
            return RedirectToPage("/Patient/Messages/Inbox");

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.PatientId == patientProfileId.Value);

        if (req == null)
        {
            TempData["StatusMessage"] = "Conversation not found.";
            return RedirectToPage("/Patient/Messages/Inbox", new { showHidden = true });
        }

        var kindNorm = NormalizeKind(kind);

        await WriteConversationVisibilityAsync(patient.Id, req.Id, kindNorm, RevealConversationAction);

        TempData["StatusMessage"] = "Conversation restored to your history.";
        return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = kindNorm });
    }

    public async Task<IActionResult> OnGetSync(string conversationKey, string? after)
    {
        var patient = await CurrentUserAsync();
        if (patient == null || string.IsNullOrWhiteSpace(conversationKey))
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        var parts = conversationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "req" || !Guid.TryParse(parts[1], out var reqId))
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        var kindPart = parts[2].ToLowerInvariant();

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null)
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reqId && r.PatientId == patientProfileId.Value);

        if (req == null)
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });

        if (kindPart == "doctor")
        {
            if (req.Status == PatientMessageRequestStatus.Pending ||
                req.Status == PatientMessageRequestStatus.AssistantChat)
            {
                return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });
            }

            if (req.Status != PatientMessageRequestStatus.ActiveDoctorChat &&
                req.Status != PatientMessageRequestStatus.Closed)
            {
                return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });
            }
        }
        else
        {
            if (req.Status != PatientMessageRequestStatus.Pending &&
                req.Status != PatientMessageRequestStatus.AssistantChat &&
                req.Status != PatientMessageRequestStatus.Closed)
            {
                return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });
            }
        }

        string? resolvedPartnerId = kindPart == "doctor"
            ? await ResolveDoctorUserIdAsync(req.DoctorProfileId)
            : req.AssistantId;

        if (string.IsNullOrWhiteSpace(resolvedPartnerId))
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        DateTime? afterUtc = null;
        if (!string.IsNullOrWhiteSpace(after) &&
            DateTimeOffset.TryParse(after, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
        {
            afterUtc = dto.UtcDateTime;
        }

        var query = _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == ThreadKey(reqId) &&
                ((m.SenderId == patient.Id && m.RecipientId == resolvedPartnerId) ||
                 (m.SenderId == resolvedPartnerId && m.RecipientId == patient.Id)));

        if (afterUtc.HasValue)
            query = query.Where(m => m.SentAt > afterUtc.Value);

        var messages = await query
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                id = m.Id,
                body = m.Body,
                sentAtUtc = m.SentAt,
                sentAtLocal = m.SentAt.ToLocalTime(),
                senderId = m.SenderId
            })
            .ToListAsync();

        await _db.InternalMessages
            .Where(m =>
                m.Subject == ThreadKey(reqId) &&
                m.RecipientId == patient.Id &&
                !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        return new JsonResult(new { messages, reloadRequired = false });
    }
}
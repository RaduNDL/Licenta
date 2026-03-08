using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Messages;

[Authorize(Roles = "Patient")]
public class InboxModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly INotificationService _notifications;

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
    public string? partnerId { get; set; }

    public Guid SelectedRequestId { get; set; }
    public string SelectedKind { get; set; } = "";
    public string? SelectedPartnerId { get; set; }
    public string SelectedUserName { get; set; } = "";
    public bool CanSend { get; set; }

    public List<ConversationVm> Conversations { get; set; } = new();
    public List<MessageVm> Messages { get; set; } = new();
    public string SelectedSubtitle { get; set; } = "";
    public string CurrentConversationKey { get; set; } = "";

    public class ConversationVm
    {
        public Guid RequestId { get; set; }
        public string Kind { get; set; } = "";
        public string? PartnerId { get; set; }
        public string PartnerName { get; set; } = "";
        public string LastMessagePreview { get; set; } = "";
    }

    public class MessageVm
    {
        public Guid Id { get; set; }
        public bool IsMine { get; set; }
        public string Body { get; set; } = "";
        public DateTime SentAtUtc { get; set; }
        public DateTime SentAtLocal { get; set; }
    }

    public class InputModel
    {
        public Guid RequestId { get; set; }
        public string Kind { get; set; } = "";
        public string? PartnerId { get; set; }
        public string NewMessageBody { get; set; } = "";
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    private static string ThreadKey(Guid id) => $"REQ:{id}";

    private async Task<ApplicationUser?> CurrentUserAsync() => await _userManager.GetUserAsync(User);

    private static string NormalizeKind(string? k)
        => (k ?? "Assistant").ToLower() switch
        {
            "doctor" => "Doctor",
            _ => "Assistant"
        };

    private static string ConversationKey(Guid reqId, string kindNorm, string? partnerId)
        => $"patient:{reqId}:{kindNorm.ToLower()}:{partnerId ?? ""}";

    private async Task<Guid?> GetPatientProfileIdAsync(string patientUserId)
        => await _db.Patients
            .Where(p => p.UserId == patientUserId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

    private async Task<string?> ResolveDoctorUserIdAsync(Guid doctorProfileId)
        => await _db.Doctors
            .Where(d => d.Id == doctorProfileId)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync();

    private async Task LoadConversationsAsync(ApplicationUser patient, Guid patientProfileId)
    {
        var requests = await _db.PatientMessageRequests
            .AsNoTracking()
            .Where(r => r.PatientId == patientProfileId &&
                        (r.Status == PatientMessageRequestStatus.ActiveDoctorChat ||
                         r.Status == PatientMessageRequestStatus.AssistantChat ||
                         r.Status == PatientMessageRequestStatus.Closed))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        foreach (var r in requests)
        {
            if (r.Status == PatientMessageRequestStatus.ActiveDoctorChat || r.Status == PatientMessageRequestStatus.Closed)
            {
                var doctorUserId = await ResolveDoctorUserIdAsync(r.DoctorProfileId);
                if (!string.IsNullOrEmpty(doctorUserId))
                    Conversations.Add(await BuildConversation(patient, r, "Doctor", doctorUserId));
            }

            if (r.Status == PatientMessageRequestStatus.AssistantChat || r.Status == PatientMessageRequestStatus.Closed)
            {
                if (!string.IsNullOrEmpty(r.AssistantId))
                    Conversations.Add(await BuildConversation(patient, r, "Assistant", r.AssistantId));
            }
        }
    }

    private async Task<ConversationVm> BuildConversation(
        ApplicationUser patient,
        PatientMessageRequest req,
        string kind,
        string partnerIdResolved)
    {
        var partner = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == partnerIdResolved);

        var thread = ThreadKey(req.Id);

        var lastMessage = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == thread &&
                (
                    (m.SenderId == patient.Id && m.RecipientId == partnerIdResolved) ||
                    (m.SenderId == partnerIdResolved && m.RecipientId == patient.Id)
                ))
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();

        var preview = lastMessage?.Body ?? req.Subject ?? "";
        if (preview.Length > 40)
            preview = preview[..40] + "...";

        return new ConversationVm
        {
            RequestId = req.Id,
            Kind = kind,
            PartnerId = partnerIdResolved,
            PartnerName =
                partner?.FullName ??
                partner?.Email ??
                partner?.UserName ??
                (kind == "Doctor" ? "Doctor" : "Assistant"),
            LastMessagePreview = preview
        };
    }

    private async Task LoadMessagesAsync(ApplicationUser patient, Guid reqId, string partnerIdResolved)
    {
        Messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m => m.Subject == ThreadKey(reqId) &&
                       ((m.SenderId == patient.Id && m.RecipientId == partnerIdResolved) ||
                        (m.SenderId == partnerIdResolved && m.RecipientId == patient.Id)))
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageVm
            {
                Id = m.Id,
                IsMine = m.SenderId == patient.Id,
                Body = m.Body,
                SentAtUtc = m.SentAt,
                SentAtLocal = m.SentAt.ToLocalTime()
            })
            .ToListAsync();

        var unread = await _db.InternalMessages
            .Where(m => m.Subject == ThreadKey(reqId) && m.RecipientId == patient.Id && !m.IsRead)
            .ToListAsync();

        if (unread.Any())
        {
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var patient = await CurrentUserAsync();
        if (patient == null) return Unauthorized();

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null) return Page();

        await LoadConversationsAsync(patient, patientProfileId.Value);

        if (requestId == Guid.Empty) return Page();

        SelectedRequestId = requestId;
        SelectedKind = NormalizeKind(kind);

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.PatientId == patientProfileId.Value);

        if (req == null) return NotFound();

        if (SelectedKind == "Doctor" && (req.Status == PatientMessageRequestStatus.AssistantChat || req.Status == PatientMessageRequestStatus.Pending))
        {
            TempData["StatusMessage"] = "This chat has been delegated to an assistant.";
            return RedirectToPage(new { requestId = req.Id, kind = "Assistant" });
        }

        string? resolvedPartnerId = null;

        if (SelectedKind == "Doctor")
        {
            resolvedPartnerId = await ResolveDoctorUserIdAsync(req.DoctorProfileId);
        }
        else
        {
            if (!string.IsNullOrEmpty(req.AssistantId))
            {
                resolvedPartnerId = req.AssistantId;
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedPartnerId))
        {
            CanSend = false;
            return Page();
        }

        SelectedPartnerId = resolvedPartnerId;

        var partner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == resolvedPartnerId);
        SelectedUserName = partner?.FullName ?? partner?.Email ?? SelectedKind;
        SelectedSubtitle = SelectedKind;

        CurrentConversationKey = ConversationKey(requestId, SelectedKind, resolvedPartnerId);

        CanSend = false;
        if (SelectedKind == "Doctor" && req.Status == PatientMessageRequestStatus.ActiveDoctorChat)
        {
            CanSend = true;
        }
        else if (SelectedKind == "Assistant" && req.Status == PatientMessageRequestStatus.AssistantChat)
        {
            CanSend = true;
        }

        await LoadMessagesAsync(patient, requestId, resolvedPartnerId);

        Input.RequestId = requestId;
        Input.Kind = SelectedKind;
        Input.PartnerId = resolvedPartnerId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var patient = await CurrentUserAsync();
        if (patient == null || string.IsNullOrWhiteSpace(Input.NewMessageBody))
            return new JsonResult(new { ok = false });

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null)
            return new JsonResult(new { ok = false });

        var req = await _db.PatientMessageRequests
            .FirstOrDefaultAsync(r => r.Id == Input.RequestId && r.PatientId == patientProfileId.Value);

        if (req == null || (req.Status != PatientMessageRequestStatus.ActiveDoctorChat && req.Status != PatientMessageRequestStatus.AssistantChat))
            return new JsonResult(new { ok = false });

        var kindNorm = NormalizeKind(Input.Kind);

        string? resolvedPartnerId = null;

        if (kindNorm == "Doctor")
        {
            resolvedPartnerId = await ResolveDoctorUserIdAsync(req.DoctorProfileId);
        }
        else
        {
            if (!string.IsNullOrEmpty(req.AssistantId))
            {
                resolvedPartnerId = req.AssistantId;
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedPartnerId))
            return new JsonResult(new { ok = false });

        var message = new InternalMessage
        {
            Id = Guid.NewGuid(),
            SenderId = patient.Id,
            RecipientId = resolvedPartnerId,
            Subject = ThreadKey(req.Id),
            Body = Input.NewMessageBody.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.InternalMessages.Add(message);
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var partnerUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == resolvedPartnerId);
        if (partnerUser != null)
        {
            var role = kindNorm == "Doctor" ? "Doctor" : "Assistant";
            await _notifications.NotifyAsync(
                partnerUser,
                NotificationType.Message,
                "New Message",
                $"You received a new message from {patient.FullName ?? patient.Email}.",
                actionUrl: $"/{role}/Messages/Inbox?requestId={req.Id}",
                actionText: "Reply",
                relatedEntity: "InternalMessage",
                relatedEntityId: message.Id.ToString()
            );
        }

        var payload = new
        {
            conversationKey = ConversationKey(req.Id, kindNorm, resolvedPartnerId),
            messageId = message.Id,
            senderId = message.SenderId,
            requestId = req.Id,
            kind = kindNorm,
            body = message.Body,
            sentAtUtc = message.SentAt
        };

        await _hub.Clients.Users(new[] { patient.Id, resolvedPartnerId }).SendAsync("message:new", payload);

        return new JsonResult(new { ok = true, messageId = message.Id, sentAtUtc = message.SentAt });
    }

    public async Task<IActionResult> OnGetSync(string conversationKey, string? after)
    {
        var patient = await CurrentUserAsync();
        if (patient == null)
            return new JsonResult(new { messages = Array.Empty<object>() });

        if (string.IsNullOrWhiteSpace(conversationKey))
            return new JsonResult(new { messages = Array.Empty<object>() });

        var parts = conversationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return new JsonResult(new { messages = Array.Empty<object>() });

        if (!Guid.TryParse(parts[1], out var reqId))
            return new JsonResult(new { messages = Array.Empty<object>() });

        var kindPart = parts[2].ToLowerInvariant();

        var patientProfileId = await GetPatientProfileIdAsync(patient.Id);
        if (patientProfileId == null)
            return new JsonResult(new { messages = Array.Empty<object>() });

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reqId && r.PatientId == patientProfileId.Value);

        if (req == null)
            return new JsonResult(new { messages = Array.Empty<object>() });

        string? resolvedPartnerId = null;

        if (kindPart == "doctor")
        {
            resolvedPartnerId = await ResolveDoctorUserIdAsync(req.DoctorProfileId);
        }
        else
        {
            if (!string.IsNullOrEmpty(req.AssistantId))
            {
                resolvedPartnerId = req.AssistantId;
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedPartnerId))
            return new JsonResult(new { messages = Array.Empty<object>() });

        DateTime? afterUtc = null;
        if (!string.IsNullOrWhiteSpace(after) &&
            DateTimeOffset.TryParse(after, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
        {
            afterUtc = dto.UtcDateTime;
        }

        var thread = ThreadKey(reqId);

        var messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == thread &&
                (afterUtc == null || m.SentAt > afterUtc.Value) &&
                (
                    (m.SenderId == patient.Id && m.RecipientId == resolvedPartnerId) ||
                    (m.SenderId == resolvedPartnerId && m.RecipientId == patient.Id)
                ))
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                messageId = m.Id,
                body = m.Body,
                sentAtUtc = m.SentAt,
                senderId = m.SenderId
            })
            .ToListAsync();

        return new JsonResult(new { messages });
    }
}
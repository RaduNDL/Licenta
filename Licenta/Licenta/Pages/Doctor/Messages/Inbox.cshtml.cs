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

namespace Licenta.Pages.Doctor.Messages;

[Authorize(Roles = "Doctor")]
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

    public record ConversationVm(
        Guid RequestId,
        string PartnerId,
        string PartnerName,
        DateTime LastAt,
        string LastPreview,
        string ConversationKey,
        bool IsActive);

    public record MessageVm(
        Guid Id,
        string Body,
        DateTime SentAtUtc,
        DateTime SentAtLocal,
        bool IsMine);

    public record AssistantVm(string Id, string Name);

    public class InputModel
    {
        public Guid RequestId { get; set; }
        public string NewMessageBody { get; set; } = "";
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<ConversationVm> Conversations { get; set; } = new();
    public List<MessageVm> Messages { get; set; } = new();
    public List<AssistantVm> Assistants { get; set; } = new();

    public string? SelectedPartnerId { get; set; }
    public string SelectedPartnerName { get; set; } = "";
    public string CurrentConversationKey { get; set; } = "";

    public Guid SelectedRequestId { get; set; }
    public bool CanSend { get; set; }
    public bool CanClose { get; set; }

    private static string ThreadKey(Guid reqId) => $"REQ:{reqId}";
    private static string ConversationKey(Guid reqId) => $"req:{reqId}:doctor";

    private bool IsAjaxRequest()
        => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(string? partnerId, Guid? requestId)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfile = await _db.Doctors
            .Include(d => d.Assistants)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);

        if (doctorProfile == null)
            return Page();

        Assistants = (doctorProfile.Assistants ?? new List<ApplicationUser>())
            .Where(a => !a.IsSoftDeleted)
            .Select(a => new AssistantVm(a.Id, a.FullName ?? a.Email ?? "Assistant"))
            .ToList();

        var hiddenPartners = await GetHiddenChatsForUser(doctorUser.Id);

        var requests = await _db.PatientMessageRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .Where(r =>
                r.DoctorProfileId == doctorProfile.Id &&
                (r.Status == PatientMessageRequestStatus.ActiveDoctorChat ||
                 r.Status == PatientMessageRequestStatus.Closed))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        foreach (var req in requests)
        {
            if (req.Patient == null || hiddenPartners.Contains(req.Patient.UserId))
                continue;

            var lastMsg = await _db.InternalMessages
                .AsNoTracking()
                .Where(m =>
                    m.Subject == ThreadKey(req.Id) &&
                    ((m.SenderId == doctorUser.Id && m.RecipientId == req.Patient.UserId) ||
                     (m.SenderId == req.Patient.UserId && m.RecipientId == doctorUser.Id)))
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            var preview = lastMsg?.Body ?? req.Subject ?? "No messages yet";
            if (preview.Length > 70) preview = preview[..70] + "...";

            var lastAt = lastMsg?.SentAt ?? (req.UpdatedAt ?? req.CreatedAt);

            Conversations.Add(new ConversationVm(
                req.Id,
                req.Patient.UserId,
                req.Patient.FullName ?? req.Patient.Email ?? "Patient",
                lastAt,
                preview,
                ConversationKey(req.Id),
                req.Status == PatientMessageRequestStatus.ActiveDoctorChat
            ));
        }

        Conversations = Conversations
            .OrderByDescending(c => c.LastAt)
            .ToList();

        PatientMessageRequest? selectedReq = null;

        if (requestId.HasValue)
        {
            selectedReq = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId.Value &&
                    r.DoctorProfileId == doctorProfile.Id &&
                    (r.Status == PatientMessageRequestStatus.ActiveDoctorChat ||
                     r.Status == PatientMessageRequestStatus.Closed));
        }
        else if (!string.IsNullOrWhiteSpace(partnerId))
        {
            selectedReq = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .AsNoTracking()
                .Where(r =>
                    r.DoctorProfileId == doctorProfile.Id &&
                    r.Patient.UserId == partnerId &&
                    (r.Status == PatientMessageRequestStatus.ActiveDoctorChat ||
                     r.Status == PatientMessageRequestStatus.Closed))
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (selectedReq == null || selectedReq.Patient == null)
            return Page();

        SelectedRequestId = selectedReq.Id;
        SelectedPartnerId = selectedReq.Patient.UserId;
        SelectedPartnerName = selectedReq.Patient.FullName ?? selectedReq.Patient.Email ?? "Patient";
        CurrentConversationKey = ConversationKey(selectedReq.Id);

        Messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == ThreadKey(selectedReq.Id) &&
                ((m.SenderId == doctorUser.Id && m.RecipientId == selectedReq.Patient.UserId) ||
                 (m.SenderId == selectedReq.Patient.UserId && m.RecipientId == doctorUser.Id)))
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageVm(
                m.Id,
                m.Body,
                m.SentAt,
                m.SentAt.ToLocalTime(),
                m.SenderId == doctorUser.Id))
            .ToListAsync();

        var unread = await _db.InternalMessages
            .Where(m =>
                m.Subject == ThreadKey(selectedReq.Id) &&
                m.RecipientId == doctorUser.Id &&
                !m.IsRead)
            .ToListAsync();

        if (unread.Count > 0)
        {
            foreach (var m in unread)
                m.IsRead = true;

            await _db.SaveChangesAsync();
        }

        CanSend = selectedReq.Status == PatientMessageRequestStatus.ActiveDoctorChat;
        CanClose = selectedReq.Status == PatientMessageRequestStatus.ActiveDoctorChat;
        Input.RequestId = selectedReq.Id;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        var body = Input.NewMessageBody?.Trim() ?? "";

        if (doctorUser == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Unauthorized." });
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Message cannot be empty." });
            TempData["StatusMessage"] = "Message cannot be empty.";
            return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = Input.RequestId });
        }

        var doctorProfileId = await _db.Doctors
            .Where(d => d.UserId == doctorUser.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (doctorProfileId == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Doctor profile not found." });
            TempData["StatusMessage"] = "Doctor profile not found.";
            return RedirectToPage("/Doctor/Messages/Inbox");
        }

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r =>
                r.Id == Input.RequestId &&
                r.DoctorProfileId == doctorProfileId.Value &&
                r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (req == null || req.Patient == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Conversation is not open." });
            TempData["StatusMessage"] = "Conversation is not open.";
            return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = Input.RequestId });
        }

        var patientUserId = req.Patient.UserId;

        var msg = new InternalMessage
        {
            Id = Guid.NewGuid(),
            SenderId = doctorUser.Id,
            RecipientId = patientUserId,
            Subject = ThreadKey(req.Id),
            Body = body,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.InternalMessages.Add(msg);
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == patientUserId);
        if (patientUser != null)
        {
            await _notifications.NotifyAsync(
                patientUser,
                NotificationType.Message,
                "New Message",
                $"You received a new message from Dr. {doctorUser.FullName ?? doctorUser.Email}.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Doctor",
                actionText: "Reply",
                relatedEntity: "InternalMessage",
                relatedEntityId: msg.Id.ToString()
            );
        }

        var payload = new
        {
            conversationKey = ConversationKey(req.Id),
            id = msg.Id,
            requestId = req.Id,
            senderId = msg.SenderId,
            body = msg.Body,
            sentAtUtc = msg.SentAt,
            sentAtLocal = msg.SentAt.ToLocalTime()
        };

        await _hub.Clients.Users(new[] { patientUserId, doctorUser.Id })
            .SendAsync("message:new", payload);

        if (!IsAjaxRequest())
            return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = req.Id });

        return new JsonResult(new
        {
            ok = true,
            id = msg.Id,
            body = msg.Body,
            senderId = msg.SenderId,
            sentAtUtc = msg.SentAt,
            sentAtLocal = msg.SentAt.ToLocalTime()
        });
    }

    public async Task<IActionResult> OnGetSync(string conversationKey, string? after)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null || string.IsNullOrWhiteSpace(conversationKey))
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        var parts = conversationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "req" || !Guid.TryParse(parts[1], out var reqId) || parts[2] != "doctor")
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        var doctorProfileId = await _db.Doctors
            .Where(d => d.UserId == doctorUser.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (doctorProfileId == null)
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == reqId && r.DoctorProfileId == doctorProfileId.Value);

        if (req == null || req.Patient == null)
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });

        if (req.Status != PatientMessageRequestStatus.ActiveDoctorChat &&
            req.Status != PatientMessageRequestStatus.Closed)
        {
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });
        }

        DateTime? afterUtc = null;
        if (!string.IsNullOrWhiteSpace(after) &&
            DateTimeOffset.TryParse(after, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
        {
            afterUtc = dto.UtcDateTime;
        }

        var messagesQuery = _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == ThreadKey(req.Id) &&
                ((m.SenderId == doctorUser.Id && m.RecipientId == req.Patient.UserId) ||
                 (m.SenderId == req.Patient.UserId && m.RecipientId == doctorUser.Id)));

        if (afterUtc.HasValue)
            messagesQuery = messagesQuery.Where(m => m.SentAt > afterUtc.Value);

        var messages = await messagesQuery
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

        var unread = await _db.InternalMessages
            .Where(m =>
                m.Subject == ThreadKey(req.Id) &&
                m.RecipientId == doctorUser.Id &&
                !m.IsRead)
            .ToListAsync();

        if (unread.Count > 0)
        {
            foreach (var m in unread)
                m.IsRead = true;

            await _db.SaveChangesAsync();
        }

        return new JsonResult(new { messages, reloadRequired = false });
    }

    public async Task<IActionResult> OnPostDelegateAsync(Guid id, string assistantId)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return RedirectToPage();

        var doctorProfile = await _db.Doctors
            .Include(d => d.Assistants)
            .FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);

        if (doctorProfile == null)
            return RedirectToPage();

        if (string.IsNullOrWhiteSpace(assistantId))
        {
            TempData["StatusMessage"] = "Please choose an assistant.";
            return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = id });
        }

        var chosen = (doctorProfile.Assistants ?? new List<ApplicationUser>())
            .FirstOrDefault(a => a.Id == assistantId && !a.IsSoftDeleted);

        if (chosen == null)
        {
            TempData["StatusMessage"] = "Invalid assistant selection.";
            return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = id });
        }

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                r.DoctorProfileId == doctorProfile.Id &&
                r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (req == null)
            return NotFound();

        req.AssistantId = chosen.Id;
        req.Status = PatientMessageRequestStatus.Pending;
        req.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            chosen,
            NotificationType.System,
            "New Delegated Request",
            $"Dr. {doctorUser.FullName ?? doctorUser.Email} has delegated an active chat to you.",
            actionUrl: "/Assistant/Messages/Requests/Index",
            actionText: "View Request",
            relatedEntity: "PatientMessageRequest",
            relatedEntityId: req.Id.ToString()
        );

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Patient.UserId);
        if (patientUser != null)
        {
            await _notifications.NotifyAsync(
                patientUser,
                NotificationType.System,
                "Chat Delegated",
                $"Dr. {doctorUser.FullName ?? doctorUser.Email} has delegated the conversation to an assistant.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant",
                actionText: "Open Chat",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString(),
                sendEmail: false
            );
        }

        TempData["StatusMessage"] = "Chat delegated to assistant.";
        return RedirectToPage("/Doctor/Messages/Inbox");
    }

    public async Task<IActionResult> OnPostCloseAsync(Guid id)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfileId = await _db.Doctors
            .Where(d => d.UserId == doctorUser.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (doctorProfileId == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                r.DoctorProfileId == doctorProfileId.Value &&
                r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (req == null)
            return RedirectToPage("/Doctor/Messages/Inbox");

        req.Status = PatientMessageRequestStatus.Closed;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Patient.UserId);
        if (patientUser != null)
        {
            await _notifications.NotifyAsync(
                patientUser,
                NotificationType.System,
                "Conversation Closed",
                $"Your conversation with Dr. {doctorUser.FullName ?? doctorUser.Email} has been closed.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Doctor",
                actionText: "View History",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );
        }

        TempData["StatusMessage"] = "Conversation closed. History is still available.";
        return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = req.Id });
    }

    public async Task<IActionResult> OnPostHideChatAsync(string partnerIdToHide)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null || string.IsNullOrWhiteSpace(partnerIdToHide))
            return Unauthorized();

        var log = new UserActivityLog
        {
            UserId = doctorUser.Id,
            Action = "HideConversation",
            EntityName = "ApplicationUser",
            EntityId = partnerIdToHide,
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { hiddenFromInbox = true })
        };

        _db.UserActivityLogs.Add(log);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Conversation hidden from your view.";
        return RedirectToPage("/Doctor/Messages/Inbox");
    }

    private async Task<List<string>> GetHiddenChatsForUser(string userId)
    {
        return await _db.UserActivityLogs
            .AsNoTracking()
            .Where(l =>
                l.UserId == userId &&
                l.Action == "HideConversation" &&
                l.EntityName == "ApplicationUser")
            .Select(l => l.EntityId)
            .Distinct()
            .ToListAsync();
    }
}
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Messages;

[Authorize(Roles = "Assistant")]
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
        string PatientUserId,
        string PatientName,
        DateTime LastAt,
        string LastMessagePreview);

    public record MessageVm(
        Guid Id,
        string Body,
        DateTime SentAtUtc,
        DateTime SentAtLocal,
        bool IsMine);

    public class InputModel
    {
        public Guid RequestId { get; set; }
        public string NewMessageBody { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public List<ConversationVm> Conversations { get; set; } = new();
    public List<MessageVm> Messages { get; set; } = new();

    public Guid SelectedRequestId { get; set; }
    public string SelectedPatientName { get; set; } = "";
    public string SelectedSubject { get; set; } = "";
    public string CurrentConversationKey { get; set; } = "";

    public bool CanSend { get; set; }
    public bool CanClose { get; set; }

    public int PendingRequestsCount { get; set; }

    private static string ThreadKey(Guid id) => $"REQ:{id}";

    public async Task OnGetAsync(Guid? requestId)
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null) return;

        if (assistant.AssignedDoctorId != null)
        {
            PendingRequestsCount = await _db.PatientMessageRequests.CountAsync(r =>
                r.DoctorProfileId == assistant.AssignedDoctorId.Value &&
                r.Status == PatientMessageRequestStatus.Pending &&
                (r.AssistantId == null || r.AssistantId == assistant.Id));
        }
        else
        {
            PendingRequestsCount = 0;
        }

        var requests = await _db.PatientMessageRequests
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .Where(r =>
                r.AssistantId == assistant.Id &&
                (r.Status == PatientMessageRequestStatus.AssistantChat || r.Status == PatientMessageRequestStatus.Closed))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        foreach (var r in requests)
        {
            var patientUserId = r.Patient.UserId;

            var last = await _db.InternalMessages
                .AsNoTracking()
                .Where(m =>
                    m.Subject == ThreadKey(r.Id) &&
                    (
                        (m.SenderId == assistant.Id && m.RecipientId == patientUserId) ||
                        (m.SenderId == patientUserId && m.RecipientId == assistant.Id)
                    ))
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            Conversations.Add(new ConversationVm(
                r.Id,
                patientUserId,
                r.Patient.FullName ?? r.Patient.Email ?? "Patient",
                last?.SentAt ?? r.CreatedAt,
                (last?.Body ?? r.Subject) ?? ""));
        }

        if (requestId.HasValue)
        {
            var req = requests.FirstOrDefault(x => x.Id == requestId.Value);
            if (req != null)
            {
                SelectedRequestId = req.Id;
                SelectedPatientName = req.Patient.FullName ?? req.Patient.Email ?? "Patient";
                SelectedSubject = req.Subject;

                CurrentConversationKey = $"patient:{req.Id}:assistant";

                CanSend = req.Status == PatientMessageRequestStatus.AssistantChat;
                CanClose = CanSend;

                var patientUserId = req.Patient.UserId;

                Messages = await _db.InternalMessages
                    .AsNoTracking()
                    .Where(m =>
                        m.Subject == ThreadKey(req.Id) &&
                        (
                            (m.SenderId == assistant.Id && m.RecipientId == patientUserId) ||
                            (m.SenderId == patientUserId && m.RecipientId == assistant.Id)
                        ))
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageVm(
                        m.Id,
                        m.Body,
                        m.SentAt,
                        m.SentAt.ToLocalTime(),
                        m.SenderId == assistant.Id))
                    .ToListAsync();

                Input.RequestId = req.Id;
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null || string.IsNullOrWhiteSpace(Input.NewMessageBody))
            return new JsonResult(new { ok = false });

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == Input.RequestId);

        if (req == null || req.AssistantId != assistant.Id || req.Status != PatientMessageRequestStatus.AssistantChat)
            return new JsonResult(new { ok = false });

        var patientUserId = req.Patient.UserId;

        var msg = new InternalMessage
        {
            Id = Guid.NewGuid(),
            SenderId = assistant.Id,
            RecipientId = patientUserId,
            Subject = ThreadKey(req.Id),
            Body = Input.NewMessageBody.Trim(),
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
                "New Message from Assistant",
                $"You received a new message from the assistant.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant",
                actionText: "Reply",
                relatedEntity: "InternalMessage",
                relatedEntityId: msg.Id.ToString()
            );
        }

        var payload = new
        {
            conversationKey = $"patient:{req.Id}:assistant",
            messageId = msg.Id,
            senderId = msg.SenderId,
            requestId = req.Id,
            body = msg.Body,
            sentAtUtc = msg.SentAt
        };

        await _hub.Clients.Users(new[] { patientUserId, assistant.Id }).SendAsync("message:new", payload);

        return new JsonResult(new { ok = true, messageId = msg.Id, sentAtUtc = msg.SentAt });
    }

    public async Task<IActionResult> OnGetSync(string conversationKey, string? after)
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null)
            return new JsonResult(new { messages = Array.Empty<object>() });

        if (string.IsNullOrWhiteSpace(conversationKey))
            return new JsonResult(new { messages = Array.Empty<object>() });

        var parts = conversationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return new JsonResult(new { messages = Array.Empty<object>() });

        if (!Guid.TryParse(parts[1], out var reqId))
            return new JsonResult(new { messages = Array.Empty<object>() });

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == reqId && r.AssistantId == assistant.Id);

        if (req == null)
            return new JsonResult(new { messages = Array.Empty<object>() });

        var patientUserId = req.Patient.UserId;

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
                    (m.SenderId == assistant.Id && m.RecipientId == patientUserId) ||
                    (m.SenderId == patientUserId && m.RecipientId == assistant.Id)
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

    public async Task<IActionResult> OnPostCloseAsync(Guid id)
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.AssistantId == assistant.Id);

        if (req == null) return NotFound();

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
                $"Your conversation with the assistant has been closed.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant",
                actionText: "View History",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );
        }

        return RedirectToPage();
    }
}
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

namespace Licenta.Pages.Assistant.Messages;

[Authorize(Roles = "Assistant")]
public class InboxModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly INotificationService _notifications;

    private const int PreviewMaxLength = 50;
    private const string ConversationVisibilityEntity = "AssistantConversation";
    private const string HideConversationAction = "HideConversationAssistant";
    private const string RevealConversationAction = "RevealConversationAssistant";

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
    public bool showHidden { get; set; }

    public record ConversationVm(
        Guid RequestId,
        string PatientUserId,
        string PatientName,
        DateTime LastAt,
        string LastMessagePreview,
        string ConversationKey,
        bool IsActive,
        bool IsClosed,
        bool IsHidden);

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

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<ConversationVm> Conversations { get; set; } = new();
    public List<MessageVm> Messages { get; set; } = new();

    public Guid SelectedRequestId { get; set; }
    public string SelectedPatientName { get; set; } = "";
    public string SelectedSubject { get; set; } = "";
    public string CurrentConversationKey { get; set; } = "";

    public bool CanSend { get; set; }
    public bool CanClose { get; set; }
    public bool ShowHidden { get; set; }
    public int PendingRequestsCount { get; set; }

    private static string ThreadKey(Guid reqId) => $"REQ:{reqId}";
    private static string ConversationKey(Guid reqId) => $"req:{reqId}:assistant";

    private bool IsAjaxRequest()
        => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private static string TruncatePreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text.Length > PreviewMaxLength
            ? text[..PreviewMaxLength] + "..."
            : text;
    }

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

    private async Task WriteConversationVisibilityAsync(string userId, Guid reqId, string action)
    {
        _db.UserActivityLogs.Add(new UserActivityLog
        {
            UserId = userId,
            Action = action,
            EntityName = ConversationVisibilityEntity,
            EntityId = ConversationKey(reqId),
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new
            {
                requestId = reqId,
                hidden = action == HideConversationAction
            })
        });

        await _db.SaveChangesAsync();
    }

    private async Task LoadConversationsAsync(
        ApplicationUser assistant,
        HashSet<string> hiddenConversationKeys,
        bool includeHidden)
    {
        Conversations.Clear();

        var requests = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .ThenInclude(p => p.User)
            .Where(r =>
                r.AssistantId == assistant.Id &&
                (r.Status == PatientMessageRequestStatus.AssistantChat ||
                 r.Status == PatientMessageRequestStatus.Closed))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        if (!requests.Any())
            return;

        var requestIds = requests.Select(r => ThreadKey(r.Id)).ToList();

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
            var threadKey = ThreadKey(r.Id);
            var convKey = ConversationKey(r.Id);
            var lastMsg = lastMessageDict.TryGetValue(threadKey, out var msg) ? msg : null;
            var preview = TruncatePreview(lastMsg?.Body ?? r.Subject);
            var isClosed = r.Status == PatientMessageRequestStatus.Closed;
            var isHidden = hiddenConversationKeys.Contains(convKey);

            var conversation = new ConversationVm(
                r.Id,
                r.Patient.UserId,
                r.Patient.FullName ?? r.Patient.Email ?? "Patient",
                lastMsg?.SentAt ?? (r.UpdatedAt ?? r.CreatedAt),
                preview,
                convKey,
                r.Status == PatientMessageRequestStatus.AssistantChat,
                isClosed,
                isHidden
            );

            if (conversation.IsHidden == includeHidden)
            {
                Conversations.Add(conversation);
            }
        }

        Conversations = Conversations
            .OrderByDescending(c => c.LastAt)
            .ThenBy(c => c.PatientName)
            .ToList();
    }

    public async Task OnGetAsync()
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null) return;

        ShowHidden = showHidden;

        PendingRequestsCount = assistant.AssignedDoctorId != null
            ? await _db.PatientMessageRequests.CountAsync(r =>
                r.DoctorProfileId == assistant.AssignedDoctorId.Value &&
                r.Status == PatientMessageRequestStatus.Pending &&
                (r.AssistantId == null || r.AssistantId == assistant.Id))
            : 0;

        var hiddenConversationKeys = await GetHiddenConversationKeysAsync(assistant.Id);
        await LoadConversationsAsync(assistant, hiddenConversationKeys, ShowHidden);

        if (requestId == Guid.Empty)
            return;

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r =>
                r.Id == requestId &&
                r.AssistantId == assistant.Id &&
                (r.Status == PatientMessageRequestStatus.AssistantChat ||
                 r.Status == PatientMessageRequestStatus.Closed));

        if (req == null)
            return;

        SelectedRequestId = req.Id;
        SelectedPatientName = req.Patient.FullName ?? req.Patient.Email ?? "Patient";
        SelectedSubject = req.Subject;
        CurrentConversationKey = ConversationKey(req.Id);

        CanSend = req.Status == PatientMessageRequestStatus.AssistantChat;
        CanClose = req.Status == PatientMessageRequestStatus.AssistantChat;

        var patientUserIdSelected = req.Patient.UserId;

        Messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                m.Subject == ThreadKey(req.Id) &&
                ((m.SenderId == assistant.Id && m.RecipientId == patientUserIdSelected) ||
                 (m.SenderId == patientUserIdSelected && m.RecipientId == assistant.Id)))
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageVm(
                m.Id,
                m.Body,
                m.SentAt,
                m.SentAt.ToLocalTime(),
                m.SenderId == assistant.Id))
            .ToListAsync();

        await _db.InternalMessages
            .Where(m =>
                m.Subject == ThreadKey(req.Id) &&
                m.RecipientId == assistant.Id &&
                !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        Input.RequestId = req.Id;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var assistant = await _userManager.GetUserAsync(User);
        var body = Input.NewMessageBody?.Trim() ?? "";

        if (assistant == null)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Unauthorized." });
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Message cannot be empty." });
            TempData["StatusMessage"] = "Message cannot be empty.";
            return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = Input.RequestId });
        }

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == Input.RequestId);

        if (req == null ||
            req.AssistantId != assistant.Id ||
            req.Status != PatientMessageRequestStatus.AssistantChat)
        {
            if (IsAjaxRequest()) return new JsonResult(new { ok = false, error = "Conversation is not open." });
            TempData["StatusMessage"] = "Conversation is not open.";
            return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = Input.RequestId });
        }

        var patientUserId = req.Patient.UserId;

        var msg = new InternalMessage
        {
            Id = Guid.NewGuid(),
            SenderId = assistant.Id,
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
                "New Message from Assistant",
                "You received a new message from the assistant.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant",
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

        await _hub.Clients.Users(new[] { patientUserId, assistant.Id })
            .SendAsync("message:new", payload);

        if (!IsAjaxRequest())
            return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });

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
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null || string.IsNullOrWhiteSpace(conversationKey))
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        var parts = conversationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "req" || !Guid.TryParse(parts[1], out var reqId) || parts[2] != "assistant")
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = false });

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == reqId && r.AssistantId == assistant.Id);

        if (req == null)
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });

        if (req.Status != PatientMessageRequestStatus.AssistantChat &&
            req.Status != PatientMessageRequestStatus.Closed)
        {
            return new JsonResult(new { messages = Array.Empty<object>(), reloadRequired = true });
        }

        var patientUserId = req.Patient.UserId;

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
                ((m.SenderId == assistant.Id && m.RecipientId == patientUserId) ||
                 (m.SenderId == patientUserId && m.RecipientId == assistant.Id)));

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
                m.RecipientId == assistant.Id &&
                !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        return new JsonResult(new { messages, reloadRequired = false });
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
                "Your conversation with the assistant has been closed.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant",
                actionText: "View History",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );
        }

        TempData["StatusMessage"] = "Conversation closed. History is still available.";
        return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
    }

    public async Task<IActionResult> OnPostHideConversationAsync(Guid requestId)
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.Id == requestId &&
                r.AssistantId == assistant.Id);

        if (req == null)
        {
            TempData["StatusMessage"] = "Conversation not found.";
            return RedirectToPage("/Assistant/Messages/Inbox");
        }

        if (req.Status != PatientMessageRequestStatus.Closed)
        {
            TempData["StatusMessage"] = "Only closed conversations can be hidden.";
            return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
        }

        await WriteConversationVisibilityAsync(assistant.Id, req.Id, HideConversationAction);

        TempData["StatusMessage"] = "Conversation hidden. Open Hidden to reveal it later.";
        return RedirectToPage("/Assistant/Messages/Inbox");
    }

    public async Task<IActionResult> OnPostRevealConversationAsync(Guid requestId)
    {
        var assistant = await _userManager.GetUserAsync(User);
        if (assistant == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.Id == requestId &&
                r.AssistantId == assistant.Id);

        if (req == null)
        {
            TempData["StatusMessage"] = "Conversation not found.";
            return RedirectToPage("/Assistant/Messages/Inbox", new { showHidden = true });
        }

        await WriteConversationVisibilityAsync(assistant.Id, req.Id, RevealConversationAction);

        TempData["StatusMessage"] = "Conversation restored to your history.";
        return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
    }
}
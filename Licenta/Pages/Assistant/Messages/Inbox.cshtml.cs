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

namespace Licenta.Pages.Assistant.Messages
{
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

        private static string ThreadKey(Guid requestId) => $"REQ:{requestId}";

        public record ConversationVm(
            Guid RequestId,
            string PatientId,
            string PatientName,
            DateTime LastAt,
            string LastMessagePreview);

        public record MessageVm(
            Guid Id,
            string Body,
            DateTime SentAtLocal,
            bool IsMine);

        public class InputModel
        {
            public Guid RequestId { get; set; }
            public string NewMessageBody { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<ConversationVm> Conversations { get; set; } = new();
        public List<MessageVm> Messages { get; set; } = new();

        public Guid SelectedRequestId { get; set; }
        public string SelectedPatientName { get; set; } = string.Empty;
        public string SelectedSubject { get; set; } = string.Empty;

        public string CurrentConversationKey { get; set; } = string.Empty;
        public bool CanSend { get; set; }
        public bool CanClose { get; set; }

        public int PendingRequestsCount { get; set; }

        public async Task OnGetAsync(Guid? requestId)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return;

            PendingRequestsCount = await _db.PatientMessageRequests
                .AsNoTracking()
                .Where(r =>
                    r.Status == PatientMessageRequestStatus.Pending &&
                    (string.IsNullOrWhiteSpace(assistant.ClinicId) ||
                     r.Patient!.ClinicId == assistant.ClinicId))
                .CountAsync();

            var requests = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Where(r =>
                    r.AssistantId == assistant.Id &&
                    r.Status == PatientMessageRequestStatus.AssistantChat)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ToListAsync();

            foreach (var r in requests)
            {
                var last = await _db.InternalMessages
                    .AsNoTracking()
                    .Where(m =>
                        m.Subject == ThreadKey(r.Id) &&
                        ((m.SenderId == assistant.Id && m.RecipientId == r.PatientId) ||
                         (m.SenderId == r.PatientId && m.RecipientId == assistant.Id)))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var preview = last?.Body ?? r.Subject ?? "";
                if (preview.Length > 40)
                    preview = preview[..40] + "...";

                Conversations.Add(new ConversationVm(
                    r.Id,
                    r.PatientId!,
                    r.Patient?.FullName ?? r.Patient?.Email ?? "Patient",
                    last?.SentAt ?? r.CreatedAt,
                    preview));
            }

            if (requestId == null)
                return;

            var req = requests.FirstOrDefault(x => x.Id == requestId.Value);
            if (req == null)
                return;

            SelectedRequestId = req.Id;
            SelectedPatientName = req.Patient?.FullName ?? req.Patient?.Email ?? "Patient";
            SelectedSubject = req.Subject;

            CurrentConversationKey = $"patient:{req.Id}:assistant";
            CanSend = true;
            CanClose = true;

            Messages = await _db.InternalMessages
                .AsNoTracking()
                .Where(m =>
                    m.Subject == ThreadKey(req.Id) &&
                    ((m.SenderId == assistant.Id && m.RecipientId == req.PatientId) ||
                     (m.SenderId == req.PatientId && m.RecipientId == assistant.Id)))
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageVm(
                    m.Id,
                    m.Body,
                    m.SentAt.ToLocalTime(),
                    m.SenderId == assistant.Id))
                .ToListAsync();

            Input.RequestId = req.Id;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            if (Input.RequestId == Guid.Empty)
                return BadRequest();

            var body = (Input.NewMessageBody ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { ok = false, error = "Message cannot be empty." }) { StatusCode = 400 };

            var req = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .FirstOrDefaultAsync(r =>
                    r.Id == Input.RequestId &&
                    r.AssistantId == assistant.Id &&
                    r.Status == PatientMessageRequestStatus.AssistantChat);

            if (req == null)
                return BadRequest();

            var msg = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = assistant.Id,
                RecipientId = req.PatientId!,
                Subject = ThreadKey(req.Id),
                Body = body,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.InternalMessages.Add(msg);
            await _db.SaveChangesAsync();

            var senderName = string.IsNullOrWhiteSpace(assistant.FullName)
                ? (assistant.Email ?? assistant.UserName ?? "Assistant")
                : assistant.FullName;

            if (req.Patient != null)
            {
                await _notifications.NotifyAsync(
                    req.Patient,
                    NotificationType.Message,
                    "New message",
                    $"New message from assistant <b>{senderName}</b>",
                    "Message",
                    req.Id.ToString()
                );
            }

            var payload = new
            {
                conversationKey = $"patient:{req.Id}:assistant",
                messageId = msg.Id,
                senderId = msg.SenderId,
                body = msg.Body,
                sentAtUtc = msg.SentAt
            };

            await _hub.Clients.Group($"USER_{req.PatientId}")
                .SendAsync("message:new", payload);

            await _hub.Clients.Group($"USER_{assistant.Id}")
                .SendAsync("message:new", payload);

            return new JsonResult(new
            {
                ok = true,
                messageId = msg.Id,
                sentAtUtc = msg.SentAt
            });
        }

        public async Task<IActionResult> OnPostCloseAsync(Guid id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await _db.PatientMessageRequests
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.AssistantId == assistant.Id &&
                    r.Status == PatientMessageRequestStatus.AssistantChat);

            if (req == null)
                return NotFound();

            req.Status = PatientMessageRequestStatus.Closed;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Conversation archived.";

            return RedirectToPage();
        }
    }
}

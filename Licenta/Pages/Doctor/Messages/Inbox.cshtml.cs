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

namespace Licenta.Pages.Doctor.Messages
{
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

        private static string ThreadKey(Guid id) => $"REQ:{id}";

        public record ConversationVm(string PartnerId, string PartnerName, DateTime LastMessageAt, string LastMessagePreview);
        public record MessageVm(Guid Id, string Body, DateTime SentAtLocal, bool IsMine);

        public class InputModel
        {
            public Guid RequestId { get; set; }
            public string RecipientId { get; set; } = "";
            public string NewMessageBody { get; set; } = "";
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<ConversationVm> Conversations { get; set; } = new();
        public List<MessageVm> Messages { get; set; } = new();

        public Guid SelectedRequestId { get; set; }
        public string SelectedUserId { get; set; } = "";
        public string SelectedUserName { get; set; } = "";

        public string CurrentConversationKey { get; set; } = "";
        public bool CanSend { get; set; }
        public bool CanClose { get; set; }

        public async Task OnGetAsync(string? userId)
        {
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null) return;

            var requests = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Where(r =>
                    r.DoctorId == doctor.Id &&
                    r.Status == PatientMessageRequestStatus.ActiveDoctorChat)
                .ToListAsync();

            foreach (var r in requests)
            {
                var last = await _db.InternalMessages
                    .Where(m => m.Subject == ThreadKey(r.Id))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                Conversations.Add(new ConversationVm(
                    r.PatientId!,
                    r.Patient?.FullName ?? r.Patient?.Email ?? "Patient",
                    last?.SentAt ?? r.CreatedAt,
                    last?.Body ?? ""));
            }

            Conversations = Conversations
                .OrderByDescending(x => x.LastMessageAt)
                .ToList();

            if (!Conversations.Any()) return;

            SelectedUserId = userId ?? Conversations.First().PartnerId;

            var req = requests.FirstOrDefault(r => r.PatientId == SelectedUserId);
            if (req == null) return;

            SelectedRequestId = req.Id;
            SelectedUserName = Conversations.First(c => c.PartnerId == SelectedUserId).PartnerName;

            Input.RequestId = SelectedRequestId;
            Input.RecipientId = SelectedUserId;

            CurrentConversationKey = $"patient:{SelectedRequestId}:doctor";
            CanSend = true;
            CanClose = true;

            Messages = await _db.InternalMessages
                .Where(m => m.Subject == ThreadKey(SelectedRequestId))
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageVm(
                    m.Id,
                    m.Body,
                    m.SentAt.ToLocalTime(),
                    m.SenderId == doctor.Id))
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null) return Unauthorized();

            var body = (Input.NewMessageBody ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { ok = false, error = "Message cannot be empty." }) { StatusCode = 400 };

            var req = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .FirstOrDefaultAsync(r =>
                    r.Id == Input.RequestId &&
                    r.DoctorId == doctor.Id &&
                    r.PatientId == Input.RecipientId &&
                    r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

            if (req == null)
                return BadRequest();

            var patient = req.Patient;
            if (patient == null)
                return BadRequest();

            var msg = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = doctor.Id,
                RecipientId = patient.Id,
                Subject = ThreadKey(Input.RequestId),
                Body = body,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.InternalMessages.Add(msg);
            await _db.SaveChangesAsync();

            var senderName = string.IsNullOrWhiteSpace(doctor.FullName)
                ? (doctor.Email ?? doctor.UserName ?? "Doctor")
                : doctor.FullName;

            await _notifications.NotifyAsync(
                patient,
                NotificationType.Message,
                "New message",
                $"New message from doctor <b>{senderName}</b>",
                "Message",
                Input.RequestId.ToString()
            );

            var payload = new
            {
                conversationKey = $"patient:{Input.RequestId}:doctor",
                messageId = msg.Id,
                senderId = msg.SenderId,
                body = msg.Body,
                sentAtUtc = msg.SentAt
            };

            await _hub.Clients.Group($"USER_{patient.Id}")
                .SendAsync("message:new", payload);

            await _hub.Clients.Group($"USER_{doctor.Id}")
                .SendAsync("message:new", payload);

            return new JsonResult(new { ok = true, messageId = msg.Id, sentAtUtc = msg.SentAt });
        }

        public async Task<IActionResult> OnPostCloseAsync(string patientId)
        {
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null) return Unauthorized();

            var req = await _db.PatientMessageRequests
                .FirstOrDefaultAsync(r =>
                    r.DoctorId == doctor.Id &&
                    r.PatientId == patientId &&
                    r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

            if (req == null) return RedirectToPage();

            req.Status = PatientMessageRequestStatus.Closed;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Conversation closed.";
            return RedirectToPage();
        }
    }
}

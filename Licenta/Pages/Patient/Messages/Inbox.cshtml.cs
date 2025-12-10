using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Messages
{
    [Authorize(Roles = "Patient")]
    public class InboxModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public InboxModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        public record ConversationVm(
            string PartnerId,
            string PartnerName,
            DateTime LastMessageAt,
            string LastMessagePreview
        );

        public record MessageVm(
            Guid Id,
            string Body,
            DateTime SentAtLocal,
            bool IsMine
        );

        public class InputModel
        {
            public string RecipientId { get; set; } = string.Empty;
            public string NewMessageBody { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<ConversationVm> Conversations { get; set; } = new();
        public List<MessageVm> Messages { get; set; } = new();

        public string SelectedUserId { get; set; } = string.Empty;
        public string SelectedUserName { get; set; } = string.Empty;

        public async Task OnGetAsync(string? userId)
        {
            var patient = await _userManager.GetUserAsync(User);
            if (patient == null)
                return;

            // Doctors for which the patient has an APPROVED request
            var approvedRequests = await _db.PatientMessageRequests
                .Include(r => r.Doctor)
                .Where(r => r.PatientId == patient.Id &&
                            r.Status == PatientMessageRequestStatus.Approved)
                .ToListAsync();

            var conversations = new List<ConversationVm>();

            foreach (var req in approvedRequests)
            {
                var doctor = req.Doctor;
                if (doctor == null) continue;

                var doctorId = doctor.Id;
                var doctorName = doctor.FullName ?? doctor.Email ?? doctor.UserName;

                // last message between patient and this doctor
                var lastMessage = await _db.InternalMessages
                    .Where(m =>
                        (m.SenderId == patient.Id && m.RecipientId == doctorId) ||
                        (m.SenderId == doctorId && m.RecipientId == patient.Id))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                DateTime lastAt = lastMessage?.SentAt ?? req.CreatedAt;
                string preview = lastMessage?.Body ?? req.Subject ?? "Message request approved";

                if (!string.IsNullOrEmpty(preview) && preview.Length > 40)
                    preview = preview[..40] + "...";

                conversations.Add(new ConversationVm(
                    doctorId,
                    doctorName,
                    lastAt,
                    preview
                ));
            }

            Conversations = conversations
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            if (!Conversations.Any())
                return;

            SelectedUserId = !string.IsNullOrEmpty(userId)
                ? userId
                : Conversations.First().PartnerId;

            var selectedConv = Conversations.FirstOrDefault(c => c.PartnerId == SelectedUserId);
            SelectedUserName = selectedConv?.PartnerName ?? "Doctor";

            Messages = await _db.InternalMessages
                .Where(m =>
                    (m.SenderId == patient.Id && m.RecipientId == SelectedUserId) ||
                    (m.SenderId == SelectedUserId && m.RecipientId == patient.Id))
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageVm(
                    m.Id,
                    m.Body,
                    m.SentAt.ToLocalTime(),
                    m.SenderId == patient.Id
                ))
                .ToListAsync();

            Input.RecipientId = SelectedUserId;
        }

        public async Task<IActionResult> OnPostAsync(string? userId)
        {
            var patient = await _userManager.GetUserAsync(User);
            if (patient == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(Input.RecipientId))
            {
                ModelState.AddModelError(string.Empty, "No doctor selected.");
            }

            if (string.IsNullOrWhiteSpace(Input.NewMessageBody))
            {
                ModelState.AddModelError(nameof(Input.NewMessageBody), "Message cannot be empty.");
            }

            var doctor = await _db.Users.FirstOrDefaultAsync(u => u.Id == Input.RecipientId);
            if (doctor == null)
            {
                ModelState.AddModelError(string.Empty, "Selected doctor does not exist.");
            }
            else
            {
                // Check if there is an approved request
                var hasPermission = await _db.PatientMessageRequests.AnyAsync(r =>
                    r.PatientId == patient.Id &&
                    r.DoctorId == Input.RecipientId &&
                    r.Status == PatientMessageRequestStatus.Approved);

                if (!hasPermission)
                {
                    ModelState.AddModelError(string.Empty, "You are not allowed to send messages to this doctor yet.");
                }
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync(Input.RecipientId);
                return Page();
            }

            var message = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = patient.Id,
                RecipientId = Input.RecipientId,
                Subject = string.Empty,
                Body = Input.NewMessageBody,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.InternalMessages.Add(message);
            await _db.SaveChangesAsync();

            var senderName = patient.FullName ?? patient.Email ?? patient.UserName;
            await _notifier.NotifyAsync(
                doctor!,
                NotificationType.Message,
                "New message from patient",
                $"You received a new message from patient <b>{senderName}</b>.",
                relatedEntity: "InternalMessage",
                relatedEntityId: message.Id.ToString()
            );

            return RedirectToPage(new { userId = Input.RecipientId });
        }
    }
}

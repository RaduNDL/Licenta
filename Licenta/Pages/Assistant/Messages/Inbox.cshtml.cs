using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return;

            var raw = await _db.InternalMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => m.SenderId == currentUser.Id || m.RecipientId == currentUser.Id)
                .ToListAsync();

            var convGroups = raw
                .Select(m => new
                {
                    PartnerId = m.SenderId == currentUser.Id ? m.RecipientId : m.SenderId,
                    PartnerName = m.SenderId == currentUser.Id
                        ? (m.Recipient.FullName ?? m.Recipient.Email ?? m.Recipient.UserName)
                        : (m.Sender.FullName ?? m.Sender.Email ?? m.Sender.UserName),
                    m.SentAt,
                    m.Body
                })
                .GroupBy(x => new { x.PartnerId, x.PartnerName })
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.SentAt).First();
                    var preview = last.Body;
                    if (!string.IsNullOrEmpty(preview) && preview.Length > 40)
                        preview = preview[..40] + "...";

                    return new ConversationVm(
                        g.Key.PartnerId,
                        g.Key.PartnerName,
                        last.SentAt,
                        preview
                    );
                })
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            Conversations = convGroups;

            if (!Conversations.Any())
                return;

            SelectedUserId = !string.IsNullOrEmpty(userId)
                ? userId
                : Conversations.First().PartnerId;

            var selectedConv = Conversations.FirstOrDefault(c => c.PartnerId == SelectedUserId);
            SelectedUserName = selectedConv?.PartnerName ?? "Conversation";

            Messages = await _db.InternalMessages
                .Where(m =>
                    (m.SenderId == currentUser.Id && m.RecipientId == SelectedUserId) ||
                    (m.SenderId == SelectedUserId && m.RecipientId == currentUser.Id))
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageVm(
                    m.Id,
                    m.Body,
                    m.SentAt.ToLocalTime(),
                    m.SenderId == currentUser.Id
                ))
                .ToListAsync();

            Input.RecipientId = SelectedUserId;
        }

        public async Task<IActionResult> OnPostAsync(string? userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(Input.RecipientId))
            {
                ModelState.AddModelError(string.Empty, "No recipient selected.");
            }

            if (string.IsNullOrWhiteSpace(Input.NewMessageBody))
            {
                ModelState.AddModelError(nameof(Input.NewMessageBody), "Message cannot be empty.");
            }

            var recipient = await _db.Users.FirstOrDefaultAsync(u => u.Id == Input.RecipientId);
            if (recipient == null)
            {
                ModelState.AddModelError(string.Empty, "Selected recipient does not exist.");
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync(Input.RecipientId);
                return Page();
            }

            var message = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = currentUser.Id,
                RecipientId = Input.RecipientId,
                Subject = string.Empty,
                Body = Input.NewMessageBody,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.InternalMessages.Add(message);
            await _db.SaveChangesAsync();

            var senderName = currentUser.FullName ?? currentUser.Email ?? currentUser.UserName;
            await _notifier.NotifyAsync(
                recipient!,
                NotificationType.Message,
                "New message received",
                $"You received a new message from <b>{senderName}</b>.",
                relatedEntity: "InternalMessage",
                relatedEntityId: message.Id.ToString()
            );

            return RedirectToPage(new { userId = Input.RecipientId });
        }
    }
}

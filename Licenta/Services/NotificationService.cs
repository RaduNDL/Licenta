using System;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Licenta.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;

        public NotificationService(AppDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        public async Task NotifyAsync(
            ApplicationUser recipient,
            NotificationType type,
            string title,
            string message,
            string? relatedEntity = null,
            string? relatedEntityId = null,
            bool sendEmail = true,
            CancellationToken ct = default)
        {
            if (recipient == null)
                throw new ArgumentNullException(nameof(recipient));

            // 1. Persist notification for in-app display
            var notification = new UserNotification
            {
                UserId = recipient.Id,
                Type = type,
                Title = title,
                Message = message,
                RelatedEntity = relatedEntity,
                RelatedEntityId = relatedEntityId,
                CreatedAtUtc = DateTime.UtcNow,
                IsRead = false
            };

            _db.UserNotifications.Add(notification);

            // 2. Optional: also create an internal message (if you use internal inbox)
            var internalMessage = new InternalMessage
            {
                Id = Guid.NewGuid(),
                // SenderId can be null or system-user. For now we mirror recipient for simplicity.
                SenderId = recipient.Id,
                RecipientId = recipient.Id,
                Subject = title,
                Body = message,
                SentAt = DateTime.UtcNow
            };

            _db.InternalMessages.Add(internalMessage);

            await _db.SaveChangesAsync(ct);

            // 3. E-mail notification
            if (sendEmail && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient.Email!, title, message);
                }
                catch
                {
                    // TODO: log error if you have logging configured
                }
            }
        }

        public Task NotifyAsync(
            ApplicationUser recipient,
            string subject,
            string htmlBody,
            CancellationToken ct = default)
        {
            return NotifyAsync(
                recipient,
                NotificationType.Info,
                subject,
                htmlBody,
                relatedEntity: null,
                relatedEntityId: null,
                sendEmail: true,
                ct: ct);
        }
    }

    /// <summary>
    /// Simple no-op e-mail sender you can register for development
    /// if you don't have a real SMTP provider configured.
    /// </summary>
    public class NoopEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => Task.CompletedTask;
    }
}

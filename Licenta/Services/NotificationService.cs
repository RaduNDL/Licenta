using System;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Licenta.Hubs;

namespace Licenta.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationService(
            AppDbContext db,
            IEmailSender emailSender,
            IHubContext<NotificationHub> hub)
        {
            _db = db;
            _emailSender = emailSender;
            _hub = hub;
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

            var internalMessage = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = recipient.Id,
                RecipientId = recipient.Id,
                Subject = title,
                Body = message,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.InternalMessages.Add(internalMessage);

            await _db.SaveChangesAsync(ct);

            await _hub.Clients.Group($"USER_{recipient.Id}")
                .SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    type = notification.Type.ToString(),
                    when = notification.CreatedAtUtc
                }, ct);

            if (sendEmail && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient.Email!, title, message);
                }
                catch
                {
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
                null,
                null,
                true,
                ct);
        }
    }

    public class NoopEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => Task.CompletedTask;
    }
}
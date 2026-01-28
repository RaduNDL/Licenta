using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub>? _hub;

        public NotificationService(AppDbContext db, IHubContext<NotificationHub>? hub = null)
        {
            _db = db;
            _hub = hub;
        }

        public async Task NotifyAsync(
            ApplicationUser user,
            NotificationType type,
            string title,
            string messageHtml,
            string? relatedEntity = null,
            string? relatedEntityId = null,
            bool sendEmail = false)
        {
            var n = new UserNotification
            {
                UserId = user.Id,
                Type = type,
                Title = title,
                Message = messageHtml,
                RelatedEntity = relatedEntity,
                RelatedEntityId = relatedEntityId,
                CreatedAtUtc = DateTime.UtcNow,
                IsRead = false
            };

            _db.UserNotifications.Add(n);
            await _db.SaveChangesAsync();

            if (_hub != null)
            {
                await _hub.Clients.Group($"USER_{user.Id}")
                    .SendAsync("notification:new", new
                    {
                        id = n.Id.ToString(),
                        title = n.Title,
                        message = n.Message,
                        type = n.Type.ToString(),
                        createdAtUtc = n.CreatedAtUtc
                    });
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _db.UserNotifications
                .CountAsync(x => x.UserId == userId && !x.IsRead);
        }

        public async Task MarkAllReadAsync(string userId)
        {
            var list = await _db.UserNotifications
                .Where(x => x.UserId == userId && !x.IsRead)
                .ToListAsync();

            if (list.Count == 0)
                return;

            foreach (var n in list)
                n.IsRead = true;

            await _db.SaveChangesAsync();

            if (_hub != null)
            {
                await _hub.Clients.Group($"USER_{userId}")
                    .SendAsync("notification:cleared");
            }
        }
    }
}

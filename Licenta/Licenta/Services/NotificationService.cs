using System;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IHubContext<NotificationHub> hub)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
    }

    public async Task NotifyAsync(
        ApplicationUser user,
        NotificationType type,
        string title,
        string message,
        string? actionUrl = null,
        string? actionText = null,
        string? relatedEntity = null,
        string? relatedEntityId = null,
        bool sendEmail = false)
    {
        if (user == null) return;

        var n = new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = type,
            Title = title ?? "",
            Message = message ?? "",
            ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
            ActionText = string.IsNullOrWhiteSpace(actionText) ? null : actionText.Trim(),
            RelatedEntity = string.IsNullOrWhiteSpace(relatedEntity) ? null : relatedEntity.Trim(),
            RelatedEntityId = string.IsNullOrWhiteSpace(relatedEntityId) ? null : relatedEntityId.Trim(),
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.UserNotifications.Add(n);
        await _db.SaveChangesAsync();

        var payload = new
        {
            id = n.Id,
            userId = n.UserId,
            createdAtUtc = n.CreatedAtUtc,
            type = n.Type.ToString(),
            title = n.Title,
            message = n.Message,
            actionUrl = n.ActionUrl,
            actionText = n.ActionText,
            isRead = n.IsRead
        };

        await _hub.Clients.User(user.Id).SendAsync("notification:new", payload);
    }

    public async Task NotifyUserAsync(string userId, string title, string actionUrl, string? actionText = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var user = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null) return;

        await NotifyAsync(
            user,
            NotificationType.General,
            title,
            "",
            actionUrl: actionUrl,
            actionText: string.IsNullOrWhiteSpace(actionText) ? "Open" : actionText
        );
    }

    public async Task MarkAllReadAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var list = await _db.UserNotifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync();

        if (list.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var n in list)
        {
            n.IsRead = true;
            n.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task MarkReadAsync(string userId, Guid notificationId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var n = await _db.UserNotifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n == null) return;

        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
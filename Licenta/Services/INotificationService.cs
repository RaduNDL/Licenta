using System;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;

namespace Licenta.Services;

public interface INotificationService
{
    Task NotifyAsync(
        ApplicationUser user,
        NotificationType type,
        string title,
        string message,
        string? actionUrl = null,
        string? actionText = null,
        string? relatedEntity = null,
        string? relatedEntityId = null,
        bool sendEmail = false);

    Task NotifyUserAsync(string userId, string title, string actionUrl, string? actionText = null);

    Task MarkAllReadAsync(string userId);

    Task MarkReadAsync(string userId, Guid notificationId);
}
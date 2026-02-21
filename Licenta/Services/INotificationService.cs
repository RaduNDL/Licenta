using Licenta.Areas.Identity.Data;
using Licenta.Models;
using System.Threading.Tasks;

namespace Licenta.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(
            ApplicationUser user,
            NotificationType type,
            string title,
            string messageHtml,
            string? relatedEntity = null,
            string? relatedEntityId = null,
            bool sendEmail = false);

        Task<int> GetUnreadCountAsync(string userId);

        Task MarkAllReadAsync(string userId);
    }
}

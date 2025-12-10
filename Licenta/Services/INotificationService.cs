using System.Threading;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;

namespace Licenta.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// Main method – creates a UserNotification, optional internal message and e-mail.
        /// </summary>
        Task NotifyAsync(
            ApplicationUser recipient,
            NotificationType type,
            string title,
            string message,
            string? relatedEntity = null,
            string? relatedEntityId = null,
            bool sendEmail = true,
            CancellationToken ct = default);

        /// <summary>
        /// Backwards-compatible overload – defaults to NotificationType.Info.
        /// </summary>
        Task NotifyAsync(
            ApplicationUser recipient,
            string subject,
            string htmlBody,
            CancellationToken ct = default);
    }
}

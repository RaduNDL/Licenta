using System.Threading;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;

namespace Licenta.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(
            ApplicationUser recipient,
            NotificationType type,
            string title,
            string message,
            string? relatedEntity = null,
            string? relatedEntityId = null,
            bool sendEmail = true,
            CancellationToken ct = default);

        Task NotifyAsync(
            ApplicationUser recipient,
            string subject,
            string htmlBody,
            CancellationToken ct = default);
    }
}
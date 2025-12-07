using System.Threading;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Licenta.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(ApplicationUser recipient, string subject, string htmlBody, CancellationToken ct = default);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;

        public NotificationService(AppDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        public async Task NotifyAsync(ApplicationUser recipient, string subject, string htmlBody, CancellationToken ct = default)
        {

            var msg = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = recipient.Id,
                RecipientId = recipient.Id,
                Subject = subject,
                Body = htmlBody,
                SentAt = DateTime.UtcNow
            };
            _db.InternalMessages.Add(msg);
            await _db.SaveChangesAsync(ct);

            try
            {
                await _emailSender.SendEmailAsync(recipient.Email, subject, htmlBody);
            }
            catch
            {
              
            }
        }
    }


    public class NoopEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }
}

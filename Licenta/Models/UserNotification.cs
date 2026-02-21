using System;

namespace Licenta.Models
{
    public class UserNotification
    {
        public long Id { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public NotificationType Type { get; set; } = NotificationType.Info;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? RelatedEntity { get; set; }
        public string? RelatedEntityId { get; set; }
    }
}

using System;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public class UserNotification
    {
        public Guid Id { get; set; }

        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public NotificationType Type { get; set; }

        public string Title { get; set; } = "";
        public string Message { get; set; } = "";

        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }

        public string? RelatedEntity { get; set; }
        public string? RelatedEntityId { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }
    }
}
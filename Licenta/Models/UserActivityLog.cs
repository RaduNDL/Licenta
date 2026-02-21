using System;

namespace Licenta.Models
{
    public class UserActivityLog
    {
        public long Id { get; set; }
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public string Action { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        public string? MetadataJson { get; set; }
    }
}

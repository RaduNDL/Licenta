using System;

namespace Licenta.Models
{
    public class AuditLog
    {
        public long Id { get; set; }
        public AuditEventType EventType { get; set; }
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? DetailsJson { get; set; }
    }
}

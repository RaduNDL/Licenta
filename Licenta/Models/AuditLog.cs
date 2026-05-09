using Licenta.Areas.Identity.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public AuditEventType EventType { get; set; }

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [MaxLength(200)]
        public string? EntityName { get; set; }

        [MaxLength(100)]
        public string? EntityId { get; set; }

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(45)] 
        public string? IpAddress { get; set; }

        [MaxLength(4000)]
        public string? DetailsJson { get; set; }
    }
}
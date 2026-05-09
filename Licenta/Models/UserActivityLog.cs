using System;
using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public class UserActivityLog
    {
        public int Id { get; set; } 

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? EntityName { get; set; }

        [MaxLength(100)]
        public string? EntityId { get; set; }

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(4000)]
        public string? MetadataJson { get; set; }
    }
}
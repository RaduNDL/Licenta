using System;
using System.ComponentModel.DataAnnotations.Schema;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public class AssistantProfile
    {
        public Guid Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string? ProfileImagePath { get; set; }
        public string? Department { get; set; }
        public string? Phone { get; set; }
        public string? Bio { get; set; }

        [NotMapped]
        public string FullName => User?.FullName ?? string.Empty;

        [NotMapped]
        public string Email => User?.Email ?? string.Empty;
    }
}
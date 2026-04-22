using System;
using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public enum ReviewTarget
    {
        Application = 0,
        Doctor = 1
    }

    public class Review
    {
        public Guid Id { get; set; }

        [Required]
        public string AuthorUserId { get; set; } = null!;
        public ApplicationUser Author { get; set; } = null!;

        [Required]
        public ReviewTarget Target { get; set; }

        public Guid? DoctorId { get; set; }
        public DoctorProfile? Doctor { get; set; }

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [StringLength(120)]
        public string? Title { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 3)]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
        public bool IsEdited => UpdatedAtUtc.HasValue;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
    }
}
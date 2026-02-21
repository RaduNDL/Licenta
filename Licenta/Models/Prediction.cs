using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Licenta.Models
{
    public enum PredictionStatus
    {
        Draft = 0,
        Validated = 1,
        Accepted = 2
    }

    public class Prediction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid PatientId { get; set; }

        [Required]
        public Guid DoctorId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public PatientProfile? Patient { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public DoctorProfile? Doctor { get; set; }

        public string? RequestedByAssistantId { get; set; }
        public ApplicationUser? RequestedByAssistant { get; set; }

        [Required]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ValidatedAtUtc { get; set; }

        [Required]
        [MaxLength(200)]
        public string ModelName { get; set; } = string.Empty;

        [MaxLength(600)]
        public string? InputSummary { get; set; }

        public string? AttachmentPath { get; set; }

        public string? InputDataJson { get; set; }
        public string? OutputDataJson { get; set; }

        [MaxLength(120)]
        public string? ResultLabel { get; set; }

        public float? Probability { get; set; }

        public string? Notes { get; set; }

        [Required]
        public PredictionStatus Status { get; set; } = PredictionStatus.Draft;
    }
}

using System;

namespace Licenta.Models
{
    public class Prediction
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string ModelName { get; set; } = string.Empty;
        public string InputSummary { get; set; } = string.Empty;

        public string? InputDataJson { get; set; }
        public string? OutputDataJson { get; set; }

        public string ResultLabel { get; set; } = "Pending";
        public double? Probability { get; set; }

        public string? AttachmentPath { get; set; }
        public string? Notes { get; set; }

        public string? RequestedByAssistantId { get; set; }
        public ApplicationUser? RequestedByAssistant { get; set; }
    }
}

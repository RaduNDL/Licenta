using System;

namespace Licenta.Models
{
    public class LabResult
    {
        public int Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public string FilePath { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;

        public LabResultStatus Status { get; set; } = LabResultStatus.Pending;
        public string? Notes { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string? UploadedByAssistantId { get; set; }
        public ApplicationUser? UploadedByAssistant { get; set; }

        public Guid? ValidatedByDoctorId { get; set; }
        public DoctorProfile? ValidatedByDoctor { get; set; }
        public DateTime? ValidatedAtUtc { get; set; }
    }
}

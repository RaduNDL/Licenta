using System;

namespace Licenta.Models
{
    public class MedicalAttachment
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        public string FilePath { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string? Type { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }

        public AttachmentStatus Status { get; set; } = AttachmentStatus.Pending;

        public string? ValidationNotes { get; set; }
        public Guid? ValidatedByDoctorId { get; set; }
        public DoctorProfile? ValidatedByDoctor { get; set; }
        public DateTime? ValidatedAtUtc { get; set; }

        public string? UploadedByAssistantId { get; set; }
        public ApplicationUser? UploadedByAssistant { get; set; }
    }
}

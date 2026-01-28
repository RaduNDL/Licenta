using System;
using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public class MedicalAttachment
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile? Patient { get; set; }

        public Guid? DoctorId { get; set; }
        public DoctorProfile? Doctor { get; set; }

        [Required, MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ContentType { get; set; }

        public DateTime UploadedAt { get; set; }
        public AttachmentStatus Status { get; set; }

        [MaxLength(2000)]
        public string? ValidationNotes { get; set; }

        [MaxLength(2000)]
        public string? PatientNotes { get; set; }

        [MaxLength(2000)]
        public string? AssistantNotes { get; set; }

        [MaxLength(2000)]
        public string? DoctorNotes { get; set; }

        public DateTime? AssignedAtUtc { get; set; }
        public string? AssignedByAssistantId { get; set; }

        public DateTime? ValidatedAtUtc { get; set; }
        public Guid? ValidatedByDoctorId { get; set; }
        public DoctorProfile? ValidatedByDoctor { get; set; }

        public string? UploadedByAssistantId { get; set; }
        public ApplicationUser? UploadedByAssistant { get; set; }
    }
}

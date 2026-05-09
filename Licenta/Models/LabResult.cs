using System;
using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public class LabResult
    {
        public int Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        [Required, MaxLength(1000)]
        public string FilePath { get; set; } = null!;

        [Required, MaxLength(260)]
        public string FileName { get; set; } = null!;

        [Required, MaxLength(200)]
        public string ContentType { get; set; } = null!;

        public LabResultStatus Status { get; set; } = LabResultStatus.Pending;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid? ValidatedByDoctorId { get; set; }
        public DoctorProfile? ValidatedByDoctor { get; set; }
        public DateTime? ValidatedAtUtc { get; set; }
    }
}
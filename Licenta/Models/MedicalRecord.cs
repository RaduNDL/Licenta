using System;

namespace Licenta.Models
{
    public class MedicalRecord
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        public DateTime VisitDateUtc { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public RecordStatus Status { get; set; } = RecordStatus.Draft;
        public DateTime? ValidatedAtUtc { get; set; }

        public string? Symptoms { get; set; }
        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }
        public string? Notes { get; set; }
    }
}

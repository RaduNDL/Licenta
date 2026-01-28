using System;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class MedicalRecord
    {
        public Guid Id { get; set; }

        [Required]
        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = default!;

        [Required]
        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = default!;

        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        [Required, StringLength(200)]
        public string Diagnosis { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Symptoms { get; set; }

        [StringLength(4000)]
        public string? Notes { get; set; }

        [StringLength(4000)]
        public string? Treatment { get; set; }

        public DateTime VisitDateUtc { get; set; }
        public RecordStatus Status { get; set; } = RecordStatus.Draft;

        public DateTime? ValidatedAtUtc { get; set; }
    }
}

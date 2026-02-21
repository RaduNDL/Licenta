using System;
using System.Collections.Generic;

namespace Licenta.Models
{
    public class Prescription
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        public Guid? MedicalRecordId { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }

        public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
        public string? Recommendations { get; set; }

        public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    }

    public class PrescriptionItem
    {
        public Guid Id { get; set; }
        public Guid PrescriptionId { get; set; }
        public Prescription Prescription { get; set; } = null!;
        public string MedicationName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? Instructions { get; set; }
        public int? Days { get; set; }
    }
}

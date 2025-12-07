using System;
using System.Collections.Generic;

namespace Licenta.Models
{
    public class PatientProfile
    {
        public Guid Id { get; set; }

        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public string? NationalId { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();
        public ICollection<MedicalAttachment> Attachments { get; set; } = new List<MedicalAttachment>();
        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    }
}

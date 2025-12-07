using System;
using System.Collections.Generic;

namespace Licenta.Models
{
    public class DoctorProfile
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public string? LicenseNumber { get; set; }

        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<MedicalAttachment> Attachments { get; set; } = new List<MedicalAttachment>();
        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public ICollection<DoctorAvailability> Availabilities { get; set; } = new List<DoctorAvailability>();
    }
}

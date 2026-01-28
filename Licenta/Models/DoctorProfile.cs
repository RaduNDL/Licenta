using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class DoctorProfile
    {
        public Guid Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        [StringLength(80)]
        public string? Specialty { get; set; }

        [StringLength(50)]
        public string? LicenseNumber { get; set; }

        [StringLength(200)]
        public string? Languages { get; set; }

        [StringLength(2000)]
        public string? Bio { get; set; }

        [StringLength(150)]
        public string? OfficeAddress { get; set; }

        [StringLength(80)]
        public string? City { get; set; }

        [StringLength(260)]
        public string? ProfileImagePath { get; set; }

        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<MedicalAttachment> Attachments { get; set; } = new List<MedicalAttachment>();
        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public ICollection<DoctorAvailability> Availabilities { get; set; } = new List<DoctorAvailability>();
    }
}

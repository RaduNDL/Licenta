using Licenta.Areas.Identity.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Licenta.Models
{
    public class PatientProfile
    {
        public Guid Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [NotMapped]
        public string FullName => User?.FullName ?? "";

        [NotMapped]
        public string Email => User?.Email ?? "";

        public ApplicationUser? User { get; set; }

        [StringLength(50)]
        public string? NationalId { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();
        public ICollection<MedicalAttachment> Attachments { get; set; } = new List<MedicalAttachment>();
        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    }
}
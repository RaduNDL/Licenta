using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using Licenta.Models;


namespace Licenta.Areas.Identity.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsSoftDeleted { get; set; }
        public string? ClinicId { get; set; }

        public string? ProfileImagePath { get; set; }

        public Guid? AssignedDoctorId { get; set; }
        public DoctorProfile? AssignedDoctor { get; set; }

        public PatientProfile? PatientProfile { get; set; }
        public DoctorProfile? DoctorProfile { get; set; }
        public Models.AssistantProfile? AssistantProfile { get; set; }
        public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
    }
}
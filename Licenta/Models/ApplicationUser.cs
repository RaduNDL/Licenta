using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace Licenta.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsSoftDeleted { get; set; }
        public string? ClinicId { get; set; }

        public PatientProfile? PatientProfile { get; set; }
        public DoctorProfile? DoctorProfile { get; set; }

        public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
    }
}

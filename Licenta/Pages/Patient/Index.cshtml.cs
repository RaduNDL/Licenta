using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string? PatientName { get; set; }
        public string? PrimaryDoctorName { get; set; }

        public int UpcomingAppointments { get; set; }
        public int TotalMedicalRecords { get; set; }
        public int TotalLabResults { get; set; }
        public int UnreadMessages { get; set; }
        public int TotalPredictions { get; set; }

        public List<AppointmentSummary> NextAppointments { get; set; } = new();
        public List<MessageSummary> LatestMessages { get; set; } = new();

        public class AppointmentSummary
        {
            public DateTime DateTimeUtc { get; set; }
            public string DateTimeLocalString =>
                DateTimeUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            public string? DoctorName { get; set; }
            public string? Location { get; set; }
            public string? Status { get; set; }
        }

        public class MessageSummary
        {
            public string SenderName { get; set; } = string.Empty;
            public string Preview { get; set; } = string.Empty;
            public DateTime SentAtUtc { get; set; }
            public string SentAtLocal =>
                SentAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            public bool IsUnread { get; set; }
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            PatientName = user.FullName ?? user.Email;

        }
    }
}

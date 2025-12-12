using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.AssistantPanel
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public string AssistantName { get; set; } = string.Empty;

        public int UpcomingAppointments { get; set; }
        public int PendingAttachments { get; set; }
        public int TotalPatients { get; set; }

        public List<AppointmentSummary> NextAppointments { get; set; } = new();

        public class AppointmentSummary
        {
            public string TimeLocal { get; set; } = string.Empty;
            public string PatientName { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                AssistantName = user.FullName ?? user.Email ?? user.UserName ?? string.Empty;
            }

            var nowUtc = DateTime.UtcNow;
            var todayUtc = nowUtc.Date;

            UpcomingAppointments = await _db.Appointments
                .Where(a => a.ScheduledAt >= nowUtc &&
                            a.Status != AppointmentStatus.Cancelled)
                .CountAsync();

            PendingAttachments = await _db.MedicalAttachments
                .Where(a => a.Status == AttachmentStatus.Pending
                            && a.Type != "AppointmentRequest")
                .CountAsync();

            TotalPatients = await _db.Patients.CountAsync();

            NextAppointments = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.ScheduledAt >= todayUtc &&
                            a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.ScheduledAt)
                .Take(5)
                .Select(a => new AppointmentSummary
                {
                    TimeLocal = a.ScheduledAt.ToLocalTime().ToString("g"),
                    PatientName = a.Patient.User.FullName
                                  ?? a.Patient.User.Email
                                  ?? a.Patient.User.UserName
                                  ?? string.Empty,
                    DoctorName = a.Doctor.User.FullName
                                 ?? a.Doctor.User.Email
                                 ?? a.Doctor.User.UserName
                                 ?? string.Empty,
                    Reason = a.Reason ?? string.Empty
                })
                .ToListAsync();
        }
    }
}

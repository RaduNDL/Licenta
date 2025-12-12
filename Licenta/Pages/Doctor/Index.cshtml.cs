using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public string? DoctorName { get; set; }
        public string? ClinicName { get; set; }

        public int TotalPatients { get; set; }
        public int TotalRecords { get; set; }
        public int UpcomingAppointments { get; set; }
        public int PendingAttachments { get; set; }
        public int TotalPredictions { get; set; }

        public List<MessageSummary> LatestMessages { get; set; } = new();

        public class MessageSummary
        {
            public string SenderName { get; set; } = string.Empty;
            public string SentAtLocal { get; set; } = string.Empty;
            public string Preview { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            var settings = await _db.SystemSettings.SingleOrDefaultAsync();
            ClinicName = settings?.ClinicName ?? "LicentaMed Clinic";

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            DoctorName = doctor?.User?.FullName
                             ?? doctor?.User?.Email
                             ?? user.FullName
                             ?? user.Email
                             ?? "Doctor";

            TotalPatients = await _db.Patients.CountAsync();

            if (doctor != null)
            {
                var doctorId = doctor.Id;

                TotalRecords = await _db.MedicalRecords
                    .CountAsync(r => r.DoctorId == doctorId);

                var now = DateTime.UtcNow;
                UpcomingAppointments = await _db.Appointments
                    .CountAsync(a => a.DoctorId == doctorId && a.ScheduledAt >= now);

                PendingAttachments = await _db.MedicalAttachments
                    .CountAsync(a => a.DoctorId == doctorId && a.Status == AttachmentStatus.Pending);

                TotalPredictions = await _db.Predictions
                    .CountAsync(p => p.DoctorId == doctorId);
            }

            LatestMessages = await _db.InternalMessages
                .Include(m => m.Sender)
                .Where(m => m.RecipientId == user.Id)
                .OrderByDescending(m => m.SentAt)
                .Take(5)
                .Select(m => new MessageSummary
                {
                    SenderName = m.Sender.FullName ?? m.Sender.Email ?? "Unknown",
                    SentAtLocal = m.SentAt.ToLocalTime().ToString("g"),
                    Preview = m.Body.Length > 80
                        ? m.Body.Substring(0, 80) + "..."
                        : m.Body
                })
                .ToListAsync();
        }
    }
}
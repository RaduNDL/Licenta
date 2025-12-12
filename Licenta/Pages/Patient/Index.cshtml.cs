using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public string PatientName { get; set; } = string.Empty;
        public string? PrimaryDoctorName { get; set; }

        public int UpcomingAppointments { get; set; }
        public int TotalMedicalRecords { get; set; }
        public int TotalLabResults { get; set; }
        public int UnreadMessages { get; set; }
        public int TotalPredictions { get; set; }

        public List<NextAppointmentVm> NextAppointments { get; set; } = new();
        public List<LatestMessageVm> LatestMessages { get; set; } = new();

        public class NextAppointmentVm
        {
            public string DateTimeLocalString { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? DoctorName { get; set; }
            public string? Location { get; set; }
        }

        public class LatestMessageVm
        {
            public string SenderName { get; set; } = string.Empty;
            public string SentAtLocal { get; set; } = string.Empty;
            public string Preview { get; set; } = string.Empty;
            public bool IsUnread { get; set; }
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return;
            }

            PatientName = user.FullName ?? user.Email ?? user.UserName ?? string.Empty;

            var patientProfile = await _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            // If no patient profile exists yet, we still show basic stats
            if (patientProfile == null)
            {
                UpcomingAppointments = 0;
                TotalMedicalRecords = 0;
                TotalLabResults = 0;
                TotalPredictions = 0;

                UnreadMessages = await _db.InternalMessages
                    .Where(m => m.RecipientId == user.Id && !m.IsRead)
                    .CountAsync();

                NextAppointments = new List<NextAppointmentVm>();
                LatestMessages = await LoadLatestMessagesAsync(user.Id);
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var todayUtc = nowUtc.Date;

            UpcomingAppointments = await _db.Appointments
                .Where(a => a.PatientId == patientProfile.Id &&
                            a.ScheduledAt >= nowUtc &&
                            a.Status != AppointmentStatus.Cancelled)
                .CountAsync();

            TotalMedicalRecords = await _db.MedicalRecords
                .Where(r => r.PatientId == patientProfile.Id)
                .CountAsync();

            TotalLabResults = await _db.LabResults
                .Where(r => r.PatientId == patientProfile.Id)
                .CountAsync();

            TotalPredictions = await _db.Predictions
                .Where(p => p.PatientId == patientProfile.Id)
                .CountAsync();

            UnreadMessages = await _db.InternalMessages
                .Where(m => m.RecipientId == user.Id && !m.IsRead)
                .CountAsync();

            // Primary doctor: last doctor from appointments (if any)
            var lastAppointment = await _db.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientProfile.Id)
                .OrderByDescending(a => a.ScheduledAt)
                .FirstOrDefaultAsync();

            if (lastAppointment?.Doctor?.User != null)
            {
                PrimaryDoctorName =
                    lastAppointment.Doctor.User.FullName ??
                    lastAppointment.Doctor.User.Email ??
                    lastAppointment.Doctor.User.UserName;
            }

            // Next few appointments
            NextAppointments = await _db.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientProfile.Id &&
                            a.ScheduledAt >= todayUtc &&
                            a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.ScheduledAt)
                .Take(5)
                .Select(a => new NextAppointmentVm
                {
                    DateTimeLocalString = a.ScheduledAt.ToLocalTime().ToString("g"),
                    Status = a.Status.ToString(),
                    DoctorName = a.Doctor != null
                        ? (a.Doctor.User.FullName ?? a.Doctor.User.Email ?? a.Doctor.User.UserName)
                        : null,
                    Location = a.Location
                })
                .ToListAsync();

            LatestMessages = await LoadLatestMessagesAsync(user.Id);
        }

        private async Task<List<LatestMessageVm>> LoadLatestMessagesAsync(string userId)
        {
            var messages = await _db.InternalMessages
                .Include(m => m.Sender)
                .Where(m => m.SenderId == userId || m.RecipientId == userId)
                .OrderByDescending(m => m.SentAt)
                .Take(5)
                .ToListAsync();

            var list = messages.Select(m =>
            {
                var senderName = m.Sender?.FullName
                                 ?? m.Sender?.Email
                                 ?? m.Sender?.UserName
                                 ?? "Unknown";

                var preview = m.Body ?? string.Empty;
                if (preview.Length > 80)
                {
                    preview = preview[..80] + "...";
                }

                return new LatestMessageVm
                {
                    SenderName = senderName,
                    SentAtLocal = m.SentAt.ToLocalTime().ToString("g"),
                    Preview = preview,
                    IsUnread = (m.RecipientId == userId && !m.IsRead)
                };
            }).ToList();

            return list;
        }
    }
}

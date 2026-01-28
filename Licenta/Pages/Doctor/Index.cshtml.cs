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
using System.Threading;
using System.Threading.Tasks;

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

        public string DoctorName { get; set; } = "Doctor";

        public int AppointmentsTodayCount { get; set; }
        public int PendingValidationsCount { get; set; }
        public int UnreadMessagesCount { get; set; }
        public int TotalPatientsCount { get; set; }
        public int PendingApprovalsCount { get; set; }
        public int PendingRescheduleApprovalsCount { get; set; }

        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<MedicalAttachment> PendingAttachments { get; set; } = new();
        public List<NotificationVm> RecentNotifications { get; set; } = new();

        public class NotificationVm
        {
            public string Type { get; set; } = "";
            public string Message { get; set; } = "";
            public string TimeAgo { get; set; } = "";
        }

        public async Task OnGetAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            DoctorName = user.FullName ?? user.Email ?? "Doctor";
            var clinicId = user.ClinicId;

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id, ct);

            if (doctor == null)
                return;

            var doctorId = doctor.Id;

            var todayLocal = DateTime.Today;
            var startLocal = todayLocal.Date;
            var endLocal = startLocal.AddDays(1);

            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            AppointmentsTodayCount = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.ScheduledAt >= startUtc
                            && a.ScheduledAt < endUtc)
                .CountAsync(ct);

            UpcomingAppointments = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.ScheduledAt >= DateTime.UtcNow
                            && a.ScheduledAt < endUtc)
                .OrderBy(a => a.ScheduledAt)
                .Take(8)
                .ToListAsync(ct);

            var attachmentsQuery = _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.Status == AttachmentStatus.Pending
                            && a.DoctorId == doctorId
                            && a.Type != "AppointmentRequest");

            if (!string.IsNullOrWhiteSpace(clinicId))
                attachmentsQuery = attachmentsQuery.Where(a => a.Patient != null && a.Patient.User != null && a.Patient.User.ClinicId == clinicId);

            PendingValidationsCount = await attachmentsQuery.CountAsync(ct);

            PendingAttachments = await attachmentsQuery
                .OrderBy(a => a.UploadedAt)
                .Take(8)
                .ToListAsync(ct);

            UnreadMessagesCount = await _db.InternalMessages
                .AsNoTracking()
                .Where(m => m.RecipientId == user.Id && !m.IsRead)
                .CountAsync(ct);

            TotalPatientsCount = await _db.Patients
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.User != null && p.User.ClinicId == clinicId)
                .CountAsync(ct);

            PendingApprovalsCount = await _db.MedicalAttachments
                .AsNoTracking()
                .Where(a => a.Type == "AppointmentRequest"
                            && a.Status == AttachmentStatus.Pending
                            && a.DoctorId == doctorId
                            && a.ValidationNotes != null
                            && EF.Functions.Like(a.ValidationNotes, "%AWAITING_DOCTOR_APPROVAL%"))
                .CountAsync(ct);

            PendingRescheduleApprovalsCount = await _db.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Where(r => r.DoctorId == doctorId
                            && r.Status == AppointmentRescheduleStatus.PatientSelected
                            && r.SelectedOptionId != null)
                .CountAsync(ct);

            var nowUtc = DateTime.UtcNow;

            var notes = await _db.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(12)
                .ToListAsync(ct);

            RecentNotifications = notes
                .Select(n => new NotificationVm
                {
                    Type = n.Type.ToString(),
                    Message = n.Message ?? "",
                    TimeAgo = FormatAgo(n.CreatedAtUtc, nowUtc)
                })
                .ToList();
        }

        private static string FormatAgo(DateTime createdAtUtc, DateTime nowUtc)
        {
            var diff = nowUtc - createdAtUtc;

            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hrs ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";

            return createdAtUtc.ToLocalTime().ToString("g");
        }
    }
}

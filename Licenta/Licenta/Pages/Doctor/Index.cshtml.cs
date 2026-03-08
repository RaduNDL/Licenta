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
        public int TodayAppointmentsCount { get; set; }
        public int PendingDocsCount { get; set; }
        public int UnreadMessagesCount { get; set; }
        public int PendingApprovalsCount { get; set; }

        public List<TodayAppointmentVm> TodayAppointments { get; set; } = new();
        public List<ActivityVm> RecentActivity { get; set; } = new();

        public class TodayAppointmentVm
        {
            public string Time { get; set; } = "";
            public string PatientName { get; set; } = "";
            public string Status { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        public class ActivityVm
        {
            public string Title { get; set; } = "";
            public string TimeAgo { get; set; } = "";
            public string IconClass { get; set; } = "";
        }

        public async Task OnGetAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            DoctorName = user.FullName ?? user.Email ?? "Doctor";

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id, ct);

            if (doctor == null)
                return;

            var doctorId = doctor.Id;
            var todayLocal = DateTime.Today;
            var startUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            TodayAppointmentsCount = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.ScheduledAt >= startUtc
                            && a.ScheduledAt < endUtc)
                .CountAsync(ct);

            var upcomingAppts = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.ScheduledAt >= startUtc
                            && a.ScheduledAt < endUtc)
                .OrderBy(a => a.ScheduledAt)
                .Take(8)
                .ToListAsync(ct);

            TodayAppointments = upcomingAppts.Select(a => new TodayAppointmentVm
            {
                Time = a.ScheduledAt.ToLocalTime().ToString("HH:mm"),
                PatientName = a.Patient?.User?.FullName ?? "Unknown Patient",
                Status = a.Status.ToString(),
                Reason = string.IsNullOrWhiteSpace(a.Reason) ? "Consultation" : a.Reason
            }).ToList();

            PendingDocsCount = await _db.MedicalAttachments
                .AsNoTracking()
                .Where(a => a.Status == AttachmentStatus.Pending
                            && a.DoctorId == doctorId
                            && a.Type != "AppointmentRequest")
                .CountAsync(ct);

            PendingApprovalsCount = await _db.MedicalAttachments
                .AsNoTracking()
                .Where(a => a.Type == "AppointmentRequest"
                            && a.Status == AttachmentStatus.Pending
                            && a.DoctorId == doctorId)
                .CountAsync(ct);

            var unreadNotifs = await _db.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .CountAsync(ct);

            UnreadMessagesCount = unreadNotifs;

            var nowUtc = DateTime.UtcNow;
            var notes = await _db.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(10)
                .ToListAsync(ct);

            RecentActivity = notes.Select(n => new ActivityVm
            {
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Type.ToString() : n.Title,
                TimeAgo = FormatAgo(n.CreatedAtUtc, nowUtc),
                IconClass = GetIconForType(n.Type.ToString())
            }).ToList();
        }

        private static string FormatAgo(DateTime createdAtUtc, DateTime nowUtc)
        {
            var diff = nowUtc - createdAtUtc;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hrs ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            return createdAtUtc.ToLocalTime().ToString("MMM dd, HH:mm");
        }

        private static string GetIconForType(string type)
        {
            return type switch
            {
                "Appointment" => "fa-calendar-check text-success",
                "Message" => "fa-envelope text-info",
                "Document" => "fa-file-medical text-primary",
                "System" => "fa-server text-secondary",
                "Prediction" => "fa-robot text-warning",
                _ => "fa-bell text-warning"
            };
        }
    }
}
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Appointments
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        public DateTime SelectedDate { get; set; }
        public List<ApptVm> Items { get; set; } = new();

        public class ApptVm
        {
            public int Id { get; set; }
            public DateTime ScheduledAtUtc { get; set; }
            public string TimeLocal { get; set; } = "";
            public string PatientName { get; set; } = "";
            public string Reason { get; set; } = "";
            public AppointmentStatus Status { get; set; }
            public VisitStage VisitStage { get; set; }
            public string StageLabel { get; set; } = "";
            public string StageCss { get; set; } = "secondary";
            public bool CanStart { get; set; }
            public bool CanFinish { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string? date)
        {
            SelectedDate = DateTime.TryParse(date, out var d) ? d.Date : DateTime.Today;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d2 => d2.UserId == user.Id);
            if (doctor == null)
                return Forbid();

            var dayStartUtc = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Local).ToUniversalTime();
            var dayEndUtc = DateTime.SpecifyKind(SelectedDate.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var list = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.Id && a.ScheduledAt >= dayStartUtc && a.ScheduledAt < dayEndUtc)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync();

            Items = list.Select(a =>
            {
                var (label, css) = MapStage(a.VisitStage, a.Status);
                var canStart = a.Status != AppointmentStatus.Cancelled
                               && a.Status != AppointmentStatus.Completed
                               && a.Status != AppointmentStatus.NoShow
                               && (a.VisitStage == VisitStage.CheckedIn || a.VisitStage == VisitStage.WaitingDoctor || a.VisitStage == VisitStage.InTriage)
                               && a.ScheduledAt.ToLocalTime().Date == DateTime.Today;

                var canFinish = a.Status != AppointmentStatus.Cancelled
                                && a.Status != AppointmentStatus.Completed
                                && a.Status != AppointmentStatus.NoShow
                                && a.VisitStage == VisitStage.InConsultation
                                && a.ScheduledAt.ToLocalTime().Date == DateTime.Today;

                return new ApptVm
                {
                    Id = a.Id,
                    ScheduledAtUtc = a.ScheduledAt,
                    TimeLocal = a.ScheduledAt.ToLocalTime().ToString("HH:mm"),
                    PatientName = a.Patient?.User?.FullName ?? a.Patient?.User?.Email ?? "Unknown",
                    Reason = string.IsNullOrWhiteSpace(a.Reason) ? "-" : a.Reason,
                    Status = a.Status,
                    VisitStage = a.VisitStage,
                    StageLabel = label,
                    StageCss = css,
                    CanStart = canStart,
                    CanFinish = canFinish
                };
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostStartAsync(int id, string? date)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d2 => d2.UserId == user.Id);
            if (doctor == null)
                return Forbid();

            var appt = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appt == null)
                return NotFound();

            if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.NoShow)
            {
                TempData["StatusMessage"] = "This appointment cannot be started.";
                return RedirectToPage(new { date });
            }

            if (!(appt.VisitStage == VisitStage.CheckedIn || appt.VisitStage == VisitStage.WaitingDoctor || appt.VisitStage == VisitStage.InTriage))
            {
                TempData["StatusMessage"] = "Patient must be checked in before starting.";
                return RedirectToPage(new { date });
            }

            appt.VisitStage = VisitStage.InConsultation;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            if (appt.Status == AppointmentStatus.Approved || appt.Status == AppointmentStatus.Pending)
                appt.Status = AppointmentStatus.Confirmed;

            await _db.SaveChangesAsync();

            var patientUser = appt.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Consultation started",
                    $"Your consultation started at <b>{appt.ScheduledAt.ToLocalTime():HH:mm}</b>.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Consultation started.";
            return RedirectToPage(new { date });
        }

        public async Task<IActionResult> OnPostFinishAsync(int id, string? date)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d2 => d2.UserId == user.Id);
            if (doctor == null)
                return Forbid();

            var appt = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appt == null)
                return NotFound();

            if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.NoShow)
            {
                TempData["StatusMessage"] = "This appointment cannot be finished.";
                return RedirectToPage(new { date });
            }

            if (appt.VisitStage != VisitStage.InConsultation)
            {
                TempData["StatusMessage"] = "Start the consultation first.";
                return RedirectToPage(new { date });
            }

            appt.VisitStage = VisitStage.Finished;
            appt.Status = AppointmentStatus.Completed;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var patientUser = appt.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Appointment completed",
                    "Your appointment was marked as completed.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Appointment completed.";
            return RedirectToPage(new { date });
        }

        private static (string label, string css) MapStage(VisitStage stage, AppointmentStatus status)
        {
            if (status == AppointmentStatus.Cancelled)
                return ("Cancelled", "secondary");

            if (status == AppointmentStatus.NoShow)
                return ("No-show", "dark");

            if (status == AppointmentStatus.Completed)
                return ("Completed", "success");

            return stage switch
            {
                VisitStage.NotArrived => ("Scheduled", "secondary"),
                VisitStage.CheckedIn => ("Checked-in", "info"),
                VisitStage.InTriage => ("Triage", "info"),
                VisitStage.WaitingDoctor => ("Waiting doctor", "warning"),
                VisitStage.InConsultation => ("In consultation", "primary"),
                VisitStage.Finished => ("Finished", "success"),
                _ => ("Scheduled", "secondary")
            };
        }
    }
}

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

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
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

        [BindProperty(SupportsGet = true, Name = "view")]
        public string ViewMode { get; set; } = "day";

        [BindProperty(SupportsGet = true, Name = "date")]
        public string? DateIso { get; set; }

        public DateTime SelectedDate { get; set; }

        public string PrevDateIso { get; set; } = "";
        public string NextDateIso { get; set; } = "";
        public string TodayIso { get; set; } = "";

        public List<AppointmentVm> WeekAppointments { get; set; } = new();
        public List<HourSlot> HourSchedule { get; set; } = new();

        public class HourSlot
        {
            public DateTime Start { get; set; }
            public List<AppointmentVm> Appointments { get; set; } = new();
        }

        public class AppointmentVm
        {
            public int Id { get; set; }
            public string PatientName { get; set; } = "";
            public string DoctorName { get; set; } = "";
            public string Reason { get; set; } = "";
            public string Status { get; set; } = "";
            public string TimeLocal { get; set; } = "";
            public DateTime ScheduledAtUtc { get; set; }
            public bool CanCancel { get; set; }

            public VisitStage VisitStage { get; set; }
            public string StageLabel { get; set; } = "";
            public string StageCss { get; set; } = "secondary";

            public bool CanCheckIn { get; set; }
            public bool CanNoShow { get; set; }
        }

        public async Task OnGetAsync()
        {
            ViewMode = ViewMode == "week" ? "week" : "day";
            SelectedDate = ParseDateOrToday(DateIso);

            TodayIso = DateTime.Today.ToString("yyyy-MM-dd");
            PrevDateIso = SafeAddDaysIso(SelectedDate, ViewMode == "week" ? -7 : -1);
            NextDateIso = SafeAddDaysIso(SelectedDate, ViewMode == "week" ? 7 : 1);

            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return;

            var clinicId = assistant.ClinicId;

            DateTime startLocal = ViewMode == "week"
                ? SelectedDate.AddDays(-(int)SelectedDate.DayOfWeek).Date
                : SelectedDate.Date;

            DateTime endLocal = ViewMode == "week"
                ? startLocal.AddDays(7)
                : startLocal.AddDays(1);

            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            var q = _db.Appointments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d2 => d2.User)
                .Where(a => a.ScheduledAt >= startUtc && a.ScheduledAt < endUtc);

            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                q = q.Where(a =>
                    a.Patient != null && a.Patient.User != null && a.Patient.User.ClinicId == clinicId &&
                    a.Doctor != null && a.Doctor.User != null && a.Doctor.User.ClinicId == clinicId);
            }

            var list = await q.OrderBy(a => a.ScheduledAt).ToListAsync();
            var nowUtc = DateTime.UtcNow;

            var vms = list.Select(a =>
            {
                var (label, css) = MapStage(a.VisitStage, a.Status);

                var canCancel = a.Status != AppointmentStatus.Cancelled
                                && a.Status != AppointmentStatus.Completed
                                && a.Status != AppointmentStatus.NoShow
                                && a.ScheduledAt >= nowUtc;

                var local = a.ScheduledAt.ToLocalTime();
                var minutesTo = (local - DateTime.Now).TotalMinutes;

                var canCheckIn = a.Status != AppointmentStatus.Cancelled
                                 && a.Status != AppointmentStatus.Completed
                                 && a.Status != AppointmentStatus.NoShow
                                 && a.VisitStage == VisitStage.NotArrived
                                 && local.Date == DateTime.Today
                                 && minutesTo <= 120
                                 && minutesTo >= -30;

                var canNoShow = a.Status != AppointmentStatus.Cancelled
                                && a.Status != AppointmentStatus.Completed
                                && a.Status != AppointmentStatus.NoShow
                                && a.VisitStage != VisitStage.Finished
                                && local.Date == DateTime.Today
                                && minutesTo < -15;

                return new AppointmentVm
                {
                    Id = a.Id,
                    ScheduledAtUtc = a.ScheduledAt,
                    TimeLocal = local.ToString("HH:mm"),
                    PatientName = a.Patient?.User?.FullName ?? a.Patient?.User?.Email ?? "Unknown",
                    DoctorName = a.Doctor?.User?.FullName ?? a.Doctor?.User?.Email ?? "Unknown",
                    Reason = string.IsNullOrWhiteSpace(a.Reason) ? "-" : a.Reason,
                    Status = a.Status.ToString(),
                    VisitStage = a.VisitStage,
                    StageLabel = label,
                    StageCss = css,
                    CanCancel = canCancel,
                    CanCheckIn = canCheckIn,
                    CanNoShow = canNoShow
                };
            }).ToList();

            HourSchedule = new();
            WeekAppointments = new();

            if (ViewMode == "day")
            {
                for (int h = 8; h < 18; h++)
                {
                    var slotStartLocal = SelectedDate.Date.AddHours(h);
                    HourSchedule.Add(new HourSlot
                    {
                        Start = slotStartLocal,
                        Appointments = vms.Where(x =>
                                x.ScheduledAtUtc.ToLocalTime().Date == SelectedDate.Date &&
                                x.ScheduledAtUtc.ToLocalTime().Hour == h)
                            .ToList()
                    });
                }
            }
            else
            {
                WeekAppointments = vms;
            }
        }

        public async Task<IActionResult> OnPostCheckInAsync(int id, string? view, string? date)
        {
            var redirectView = view == "week" ? "week" : "day";
            var redirectDate = ParseDateOrToday(date).ToString("yyyy-MM-dd");

            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var appt = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appt == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                var ok = appt.Patient?.User?.ClinicId == assistant.ClinicId && appt.Doctor?.User?.ClinicId == assistant.ClinicId;
                if (!ok)
                    return Forbid();
            }

            if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.NoShow)
            {
                TempData["StatusMessage"] = "This appointment cannot be checked in.";
                return RedirectToPage(new { view = redirectView, date = redirectDate });
            }

            if (appt.VisitStage != VisitStage.NotArrived)
            {
                TempData["StatusMessage"] = "Patient is already checked in (or beyond).";
                return RedirectToPage(new { view = redirectView, date = redirectDate });
            }

            var local = appt.ScheduledAt.ToLocalTime();
            var minutesTo = (local - DateTime.Now).TotalMinutes;

            if (local.Date != DateTime.Today || minutesTo > 120 || minutesTo < -30)
            {
                TempData["StatusMessage"] = "Check-in is allowed only close to the appointment time (today).";
                return RedirectToPage(new { view = redirectView, date = redirectDate });
            }

            appt.VisitStage = VisitStage.CheckedIn;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            if (appt.Status == AppointmentStatus.Approved || appt.Status == AppointmentStatus.Pending)
                appt.Status = AppointmentStatus.Confirmed;

            await _db.SaveChangesAsync();

            var patientUser = appt.Patient?.User;
            var doctorUser = appt.Doctor?.User;

            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Checked in",
                    $"You were checked in for your appointment at <b>{local:HH:mm}</b>.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(
                    doctorUser,
                    NotificationType.Appointment,
                    "Patient checked in",
                    $"Patient <b>{patientUser?.FullName ?? patientUser?.Email}</b> checked in for <b>{local:HH:mm}</b>.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Patient checked in.";
            return RedirectToPage(new { view = redirectView, date = redirectDate });
        }

        public async Task<IActionResult> OnPostNoShowAsync(int id, string? view, string? date)
        {
            var redirectView = view == "week" ? "week" : "day";
            var redirectDate = ParseDateOrToday(date).ToString("yyyy-MM-dd");

            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var appt = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appt == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                var ok = appt.Patient?.User?.ClinicId == assistant.ClinicId && appt.Doctor?.User?.ClinicId == assistant.ClinicId;
                if (!ok)
                    return Forbid();
            }

            if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.NoShow)
            {
                TempData["StatusMessage"] = "This appointment cannot be marked as no-show.";
                return RedirectToPage(new { view = redirectView, date = redirectDate });
            }

            var local = appt.ScheduledAt.ToLocalTime();
            if (local.Date != DateTime.Today)
            {
                TempData["StatusMessage"] = "No-show can be marked only for today's appointments.";
                return RedirectToPage(new { view = redirectView, date = redirectDate });
            }

            if (appt.ScheduledAt > DateTime.UtcNow.AddMinutes(-15))
            {
                TempData["StatusMessage"] = "Too early to mark no-show (wait at least 15 minutes after scheduled time).";
                return RedirectToPage(new { view = redirectView, date = redirectDate });
            }

            appt.Status = AppointmentStatus.NoShow;
            appt.VisitStage = VisitStage.Finished;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var patientUser = appt.Patient?.User;
            var doctorUser = appt.Doctor?.User;

            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Marked as no-show",
                    $"Your appointment at <b>{local:HH:mm}</b> was marked as <b>No-show</b>.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(
                    doctorUser,
                    NotificationType.Appointment,
                    "No-show",
                    $"Appointment at <b>{local:HH:mm}</b> was marked as <b>No-show</b> for patient <b>{patientUser?.FullName ?? patientUser?.Email}</b>.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Marked as no-show.";
            return RedirectToPage(new { view = redirectView, date = redirectDate });
        }

        private static DateTime ParseDateOrToday(string? iso)
        {
            if (!string.IsNullOrWhiteSpace(iso) && DateTime.TryParse(iso, out var d))
            {
                var dt = d.Date;
                if (dt.Year >= 2000 && dt.Year <= 2100)
                    return dt;
            }

            return DateTime.Today;
        }

        private static string SafeAddDaysIso(DateTime d, int days)
        {
            try
            {
                var x = d.AddDays(days);
                return x.ToString("yyyy-MM-dd");
            }
            catch
            {
                return DateTime.Today.ToString("yyyy-MM-dd");
            }
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

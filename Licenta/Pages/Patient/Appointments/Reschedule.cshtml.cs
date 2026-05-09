using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PatientProfileEntity = Licenta.Models.PatientProfile;

namespace Licenta.Pages.Patient.Appointments
{
    [Authorize(Roles = "Patient")]
    public class RescheduleModel : PageModel
    {
        private const int SlotMinutes = 30;
        private const int DurationMinutes = 30;
        private const int DaysAhead = 14;

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public RescheduleModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Please provide a reason for the reschedule.")]
        [StringLength(500)]
        public string InputReason { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please select a new time slot.")]
        public string SelectedSlotKey { get; set; } = string.Empty;

        [BindProperty]
        public int? SelectedOptionId { get; set; }

        public enum PageViewMode { SlotPicker, ProposedOptions, RequestPending }

        public PageViewMode ViewMode { get; set; } = PageViewMode.SlotPicker;
        public bool CanRequest { get; set; }

        public string DoctorName { get; set; } = "Doctor";
        public string CurrentScheduledAtDisplay { get; set; } = "";
        public string Location { get; set; } = "Clinic";
        public string DoctorSlotsJson { get; set; } = "{}";

        public AppointmentRescheduleRequest? PendingRequest { get; set; }
        public List<AppointmentRescheduleOption> ProposedOptions { get; set; } = new();

        public class SlotUiModel
        {
            public string Key { get; set; } = "";
            public string TimeDisplay { get; set; } = "";
            public string LocalIso { get; set; } = "";
        }

        public class DoctorUiModel
        {
            public Guid Id { get; set; }
            public Dictionary<string, List<SlotUiModel>> DaysAndSlots { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var patient = await _db.Set<PatientProfileEntity>()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return Forbid();

            var appt = await _db.Set<Appointment>()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == Id && a.PatientId == patient.Id);

            if (appt == null) return NotFound();

            DoctorName = appt.Doctor?.User?.FullName ?? appt.Doctor?.User?.Email ?? "Doctor";
            CurrentScheduledAtDisplay = appt.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Location = string.IsNullOrWhiteSpace(appt.Location) ? "Clinic" : appt.Location;

            await DetermineViewModeAsync(appt, patient.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var patient = await _db.Set<PatientProfileEntity>()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return Forbid();

            var appt = await _db.Set<Appointment>()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == Id && a.PatientId == patient.Id);

            if (appt == null) return NotFound();

            if (!await CanRequestRescheduleAsync(appt.Id))
            {
                TempData["StatusMessage"] = "You cannot request a reschedule for this appointment.";
                return RedirectToPage(new { id = Id });
            }

            if (!ModelState.IsValid)
            {
                await RepopulateForStep1Async(appt);
                return Page();
            }

            var parts = SelectedSlotKey.Split('|');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var doctorId) || doctorId != appt.DoctorId)
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "Invalid slot selected.");
                await RepopulateForStep1Async(appt);
                return Page();
            }

            var localIso = parts[1];
            if (!DateTime.TryParseExact(localIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDt))
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "Invalid slot time format.");
                await RepopulateForStep1Async(appt);
                return Page();
            }

            var scheduledUtc = DateTime.SpecifyKind(localDt, DateTimeKind.Local).ToUniversalTime();

            if (scheduledUtc <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "You cannot choose a past slot.");
                await RepopulateForStep1Async(appt);
                return Page();
            }

            if (scheduledUtc == appt.ScheduledAt)
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "You cannot reschedule to the same time as your current appointment.");
                await RepopulateForStep1Async(appt);
                return Page();
            }

            var alreadyExists = await _db.Set<AppointmentRescheduleRequest>()
                .AnyAsync(r =>
                    r.AppointmentId == appt.Id &&
                    (r.Status == AppointmentRescheduleStatus.Requested ||
                     r.Status == AppointmentRescheduleStatus.Proposed ||
                     r.Status == AppointmentRescheduleStatus.PatientSelected));

            if (alreadyExists)
            {
                TempData["StatusMessage"] = "There is already an active reschedule request for this appointment.";
                return RedirectToPage("/Patient/Appointments/Index");
            }

            var req = new AppointmentRescheduleRequest
            {
                AppointmentId = appt.Id,
                PatientId = patient.Id,
                DoctorId = appt.DoctorId,
                Status = AppointmentRescheduleStatus.Requested,
                Reason = InputReason.Trim(),
                PreferredWindows = localIso,
                OldScheduledAtUtc = appt.ScheduledAt,
                NewScheduledAtUtc = DateTime.SpecifyKind(scheduledUtc, DateTimeKind.Utc),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Add(req);
            await _db.SaveChangesAsync();

            var doctorUser = appt.Doctor?.User;
            if (doctorUser != null)
            {
                var localKinded = localDt;
                await _notifier.NotifyAsync(
                    doctorUser,
                    NotificationType.Appointment,
                    "Reschedule Request",
                    $"Patient {user.FullName} requested to reschedule to <b>{localKinded:ddd dd MMM HH:mm}</b>.",
                    actionUrl: "/Doctor/Appointments/RescheduleApprovals",
                    actionText: "View Reschedules",
                    relatedEntity: "AppointmentRescheduleRequest",
                    relatedEntityId: req.Id.ToString(),
                    sendEmail: false
                );
            }

            await _notifier.NotifyAsync(
                user,
                NotificationType.Appointment,
                "Reschedule request submitted",
                $"Your reschedule request was sent to Dr. {appt.Doctor?.User?.FullName}.",
                actionUrl: "/Patient/Appointments/Index",
                actionText: "View Appointments",
                relatedEntity: "AppointmentRescheduleRequest",
                relatedEntityId: req.Id.ToString(),
                sendEmail: false
            );

            TempData["StatusMessage"] = "Reschedule request submitted successfully.";
            return RedirectToPage("/Patient/Appointments/Index");
        }

        public async Task<IActionResult> OnPostSelectOptionAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var patient = await _db.Set<PatientProfileEntity>()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return Forbid();

            var appt = await _db.Set<Appointment>()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == Id && a.PatientId == patient.Id);

            if (appt == null) return NotFound();

            if (SelectedOptionId == null)
            {
                TempData["StatusMessage"] = "Please select one of the proposed time slots.";
                return RedirectToPage(new { id = Id });
            }

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Options)
                .FirstOrDefaultAsync(r =>
                    r.AppointmentId == appt.Id &&
                    r.PatientId == patient.Id &&
                    r.Status == AppointmentRescheduleStatus.Proposed);

            if (req == null)
            {
                TempData["StatusMessage"] = "No active proposal found for this appointment.";
                return RedirectToPage("/Patient/Appointments/Index");
            }

            var chosenOption = req.Options.FirstOrDefault(o => o.Id == SelectedOptionId.Value);
            if (chosenOption == null)
            {
                TempData["StatusMessage"] = "Invalid option selected.";
                return RedirectToPage(new { id = Id });
            }

            foreach (var opt in req.Options)
                opt.IsChosen = (opt.Id == chosenOption.Id);

            req.SelectedOptionId = chosenOption.Id;
            req.NewScheduledAtUtc = DateTime.SpecifyKind(chosenOption.ProposedStartUtc, DateTimeKind.Utc);
            req.Status = AppointmentRescheduleStatus.PatientSelected;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var doctorUser = appt.Doctor?.User;
            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(
                    doctorUser,
                    NotificationType.Appointment,
                    "Patient selected a reschedule slot",
                    $"Patient {user.FullName} selected <b>{chosenOption.ProposedStartUtc.ToLocalTime():ddd dd MMM HH:mm}</b> from your proposed options.",
                    actionUrl: $"/Doctor/Appointments/RescheduleReview?requestId={req.Id}",
                    actionText: "Review & Approve",
                    relatedEntity: "AppointmentRescheduleRequest",
                    relatedEntityId: req.Id.ToString(),
                    sendEmail: false
                );
            }

            await _notifier.NotifyAsync(
                user,
                NotificationType.Appointment,
                "Slot selected — waiting for approval",
                $"You selected <b>{chosenOption.ProposedStartUtc.ToLocalTime():ddd dd MMM HH:mm}</b>. Waiting for the doctor to confirm.",
                actionUrl: "/Patient/Appointments/Index",
                actionText: "View Appointments",
                relatedEntity: "AppointmentRescheduleRequest",
                relatedEntityId: req.Id.ToString(),
                sendEmail: false
            );

            TempData["StatusMessage"] = "Your selection was submitted. Waiting for the doctor to confirm.";
            return RedirectToPage("/Patient/Appointments/Index");
        }

        private async Task DetermineViewModeAsync(Appointment appt, Guid patientId)
        {
            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Options)
                .FirstOrDefaultAsync(r =>
                    r.AppointmentId == appt.Id &&
                    r.PatientId == patientId &&
                    (r.Status == AppointmentRescheduleStatus.Requested ||
                     r.Status == AppointmentRescheduleStatus.Proposed ||
                     r.Status == AppointmentRescheduleStatus.PatientSelected));

            if (req == null)
            {
                CanRequest = await CanRequestRescheduleAsync(appt.Id);
                if (CanRequest)
                {
                    await LoadDoctorSlotsAsync(appt.DoctorId, appt.Id, appt.ScheduledAt);
                }
                ViewMode = PageViewMode.SlotPicker;
                return;
            }

            PendingRequest = req;

            if (req.Status == AppointmentRescheduleStatus.Proposed && req.Options.Any())
            {
                ProposedOptions = req.Options
                    .OrderBy(o => o.ProposedStartUtc)
                    .ToList();
                ViewMode = PageViewMode.ProposedOptions;
            }
            else
            {
                ViewMode = PageViewMode.RequestPending;
            }
        }

        private async Task RepopulateForStep1Async(Appointment appt)
        {
            DoctorName = appt.Doctor?.User?.FullName ?? appt.Doctor?.User?.Email ?? "Doctor";
            CurrentScheduledAtDisplay = appt.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Location = string.IsNullOrWhiteSpace(appt.Location) ? "Clinic" : appt.Location;
            CanRequest = true;
            ViewMode = PageViewMode.SlotPicker;
            await LoadDoctorSlotsAsync(appt.DoctorId, appt.Id, appt.ScheduledAt);
        }

        private async Task LoadDoctorSlotsAsync(Guid doctorId, int appointmentId, DateTime currentScheduledAtUtc)
        {
            var schedule = await _db.DoctorAvailabilities
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.IsActive)
                .ToListAsync();

            var slots = schedule.Count > 0
                ? await BuildSlotsAsync(doctorId, schedule, appointmentId, currentScheduledAtUtc)
                : new List<string>();

            var docUi = new DoctorUiModel { Id = doctorId };

            if (slots.Count > 0)
            {
                var grouped = slots.GroupBy(s => DateTime.Parse(s).ToString("yyyy-MM-dd"));
                foreach (var g in grouped)
                {
                    docUi.DaysAndSlots[g.Key] = g.Select(s => new SlotUiModel
                    {
                        Key = $"{doctorId:N}|{s}",
                        TimeDisplay = DateTime.Parse(s).ToString("HH:mm"),
                        LocalIso = s
                    }).ToList();
                }
            }

            DoctorSlotsJson = JsonSerializer.Serialize(docUi);
        }

        private async Task<List<string>> BuildSlotsAsync(
            Guid doctorId,
            List<DoctorAvailability> schedule,
            int currentAppointmentId,
            DateTime currentScheduledAtUtc)
        {
            var result = new List<string>();
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(DaysAhead);

            var rangeStartUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Local).ToUniversalTime();
            var rangeEndUtc = DateTime.SpecifyKind(endDate.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var existingAppointments = await _db.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.DoctorId == doctorId &&
                    a.Id != currentAppointmentId &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.Rejected &&
                    a.ScheduledAt >= rangeStartUtc &&
                    a.ScheduledAt < rangeEndUtc)
                .ToListAsync();

            var proposedStartsUtc = await _db.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Where(r =>
                    r.DoctorId == doctorId &&
                    r.AppointmentId != currentAppointmentId &&
                    r.Status == AppointmentRescheduleStatus.Requested &&
                    r.NewScheduledAtUtc.HasValue &&
                    r.NewScheduledAtUtc >= rangeStartUtc &&
                    r.NewScheduledAtUtc < rangeEndUtc)
                .Select(r => r.NewScheduledAtUtc!.Value)
                .ToListAsync();

            for (var day = startDate; day <= endDate; day = day.AddDays(1))
            {
                var daySlots = schedule.Where(s => s.IsActive && s.DayOfWeek == day.DayOfWeek).ToList();

                foreach (var s in daySlots)
                {
                    for (var t = s.StartTime; t < s.EndTime; t = t.Add(TimeSpan.FromMinutes(SlotMinutes)))
                    {
                        var local = DateTime.SpecifyKind(day.Date + t, DateTimeKind.Local);
                        var utc = local.ToUniversalTime();
                        if (utc <= DateTime.UtcNow) continue;

                        if (utc == currentScheduledAtUtc) continue;

                        var newEndUtc = utc.AddMinutes(DurationMinutes);

                        if (existingAppointments.Any(a => a.ScheduledAt < newEndUtc && a.ScheduledAt.AddMinutes(DurationMinutes) > utc))
                            continue;

                        if (proposedStartsUtc.Any(p => p < newEndUtc && p.AddMinutes(DurationMinutes) > utc))
                            continue;

                        result.Add(local.ToString("yyyy-MM-ddTHH:mm"));
                    }
                }
            }

            return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        private async Task<bool> CanRequestRescheduleAsync(int appointmentId)
        {
            var appt = await _db.Set<Appointment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appt == null) return false;

            if (appt.Status == AppointmentStatus.Cancelled ||
                appt.Status == AppointmentStatus.Completed ||
                appt.Status == AppointmentStatus.Rejected)
                return false;

            if (appt.ScheduledAt <= DateTime.UtcNow)
                return false;

            var exists = await _db.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .AnyAsync(r =>
                    r.AppointmentId == appointmentId &&
                    (r.Status == AppointmentRescheduleStatus.Requested
                     || r.Status == AppointmentRescheduleStatus.Proposed
                     || r.Status == AppointmentRescheduleStatus.PatientSelected));

            return !exists;
        }
    }
}
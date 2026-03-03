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

        public bool CanRequest { get; set; }

        public string DoctorName { get; set; } = "Doctor";
        public string CurrentScheduledAtDisplay { get; set; } = "";
        public string Location { get; set; } = "Clinic";
        public string DoctorSlotsJson { get; set; } = "{}";

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

            CanRequest = await CanRequestRescheduleAsync(appt.Id);

            if (CanRequest)
            {
                await LoadDoctorSlotsAsync(appt.DoctorId);
            }

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

            var canRequest = await CanRequestRescheduleAsync(appt.Id);

            if (!canRequest)
            {
                TempData["StatusMessage"] = "You cannot request a reschedule for this appointment.";
                return RedirectToPage(new { id = Id });
            }

            if (!ModelState.IsValid)
            {
                DoctorName = appt.Doctor?.User?.FullName ?? appt.Doctor?.User?.Email ?? "Doctor";
                CurrentScheduledAtDisplay = appt.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                Location = string.IsNullOrWhiteSpace(appt.Location) ? "Clinic" : appt.Location;
                CanRequest = true;
                await LoadDoctorSlotsAsync(appt.DoctorId);
                return Page();
            }

            var parts = SelectedSlotKey.Split('|');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var doctorId) || doctorId != appt.DoctorId)
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "Invalid slot selected.");
                await LoadDoctorSlotsAsync(appt.DoctorId);
                return Page();
            }

            var localIso = parts[1];
            if (!DateTime.TryParseExact(localIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDt))
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "Invalid slot time format.");
                await LoadDoctorSlotsAsync(appt.DoctorId);
                return Page();
            }

            var localKinded = DateTime.SpecifyKind(localDt, DateTimeKind.Local);
            var scheduledUtc = localKinded.ToUniversalTime();

            if (scheduledUtc <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(SelectedSlotKey), "You cannot choose a past slot.");
                await LoadDoctorSlotsAsync(appt.DoctorId);
                return Page();
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
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Add(req);
            await _db.SaveChangesAsync();

            var doctorUser = appt.Doctor?.User;
            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(
                    doctorUser,
                    NotificationType.Appointment,
                    "Reschedule Request",
                    $"Patient {user.FullName} requested to reschedule an appointment.",
                    actionUrl: "/Doctor/Appointments/Index",
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

        private async Task LoadDoctorSlotsAsync(Guid doctorId)
        {
            var schedule = await _db.DoctorAvailabilities.AsNoTracking().Where(x => x.DoctorId == doctorId && x.IsActive).ToListAsync();
            var slots = new List<string>();

            if (schedule.Count > 0)
            {
                slots = await BuildSlotsAsync(doctorId, schedule);
            }

            var docUi = new DoctorUiModel
            {
                Id = doctorId
            };

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

        private async Task<List<string>> BuildSlotsAsync(Guid doctorId, List<DoctorAvailability> schedule)
        {
            var result = new List<string>();
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(DaysAhead);

            var rangeStartUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Local).ToUniversalTime();
            var rangeEndUtc = DateTime.SpecifyKind(endDate.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var existingAppointments = await _db.Appointments.AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected && a.ScheduledAt >= rangeStartUtc && a.ScheduledAt < rangeEndUtc)
                .ToListAsync();

            var proposedStartsUtc = await GetProposedStartsUtcAsync(doctorId, rangeStartUtc, rangeEndUtc);

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

                        var newStartUtc = utc;
                        var newEndUtc = utc.AddMinutes(DurationMinutes);

                        if (existingAppointments.Any(a => a.ScheduledAt < newEndUtc && a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc)) continue;
                        if (proposedStartsUtc.Any(p => p < newEndUtc && p.AddMinutes(DurationMinutes) > newStartUtc)) continue;

                        result.Add(local.ToString("yyyy-MM-ddTHH:mm"));
                    }
                }
            }
            return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        private async Task<List<DateTime>> GetProposedStartsUtcAsync(Guid doctorId, DateTime rangeStartUtc, DateTime rangeEndUtc)
        {
            var items = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => a.Type == "AppointmentRequest" && a.Status == AttachmentStatus.Pending && a.DoctorId == doctorId && a.ValidationNotes != null && a.ValidationNotes.Contains("Suggested:"))
                .Select(a => a.ValidationNotes!).ToListAsync();

            var result = new List<DateTime>();
            foreach (var notes in items)
            {
                var iso = ExtractSuggestedIso(notes);
                if (!string.IsNullOrWhiteSpace(iso) && DateTime.TryParseExact(iso, new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    var utc = DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
                    if (utc >= rangeStartUtc && utc < rangeEndUtc) result.Add(utc);
                }
            }
            return result;
        }

        private static string ExtractSuggestedIso(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";
            var idx = notes.IndexOf("Suggested:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            var v = notes[(idx + "Suggested:".Length)..].Trim();
            if (v.Contains("|")) v = v.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return string.IsNullOrWhiteSpace(v) ? "" : v;
        }

        private async Task<bool> CanRequestRescheduleAsync(int appointmentId)
        {
            var appt = await _db.Set<Appointment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appt == null) return false;

            if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Completed)
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
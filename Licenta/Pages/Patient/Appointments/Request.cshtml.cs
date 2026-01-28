using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PatientProfileEntity = Licenta.Models.PatientProfile;

namespace Licenta.Pages.Patient.Appointments
{
    [Authorize(Roles = "Patient")]
    public class RequestModel : PageModel
    {
        private const int SlotMinutes = 30;
        private const int DurationMinutes = 30;
        private const int DaysAhead = 14;

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public RequestModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
            _notifier = notifier;
        }

        public class SlotOptionVm
        {
            public string Key { get; set; } = "";
            public string Display { get; set; } = "";
            public Guid DoctorId { get; set; }
            public string LocalIso { get; set; } = "";
        }

        private class AppointmentRequestPayload
        {
            public DateTime CreatedAtUtc { get; set; }
            public string? ClinicId { get; set; }
            public string PatientUserId { get; set; } = "";
            public Guid PatientProfileId { get; set; }
            public Guid SelectedDoctorId { get; set; }
            public string SelectedLocalIso { get; set; } = "";
            public DateTime SelectedScheduledUtc { get; set; }
            public string Reason { get; set; } = "";
        }

        public List<SlotOptionVm> AvailableSlots { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string SelectedSlotKey { get; set; } = "";

            [Required, MaxLength(2000)]
            public string Reason { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            await LoadSlotsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadSlotsAsync();

            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Please fix form errors.";
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return Page();
            }

            var picked = AvailableSlots.FirstOrDefault(s => s.Key == Input.SelectedSlotKey);
            if (picked == null)
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "Selected slot is no longer available.");
                return Page();
            }

            if (!DateTime.TryParseExact(picked.LocalIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDt))
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "Invalid slot.");
                return Page();
            }

            var localKinded = DateTime.SpecifyKind(localDt, DateTimeKind.Local);
            var scheduledUtc = localKinded.ToUniversalTime();

            if (scheduledUtc <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "You cannot choose a past slot.");
                return Page();
            }

            var ok = await ValidateSlotStillAvailableAsync(picked.DoctorId, localKinded);
            if (!ok)
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "Selected slot is no longer available.");
                return Page();
            }

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null)
            {
                patient = new PatientProfileEntity { Id = Guid.NewGuid(), UserId = user.Id };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync();
            }

            var payloadObj = new AppointmentRequestPayload
            {
                CreatedAtUtc = DateTime.UtcNow,
                ClinicId = user.ClinicId,
                PatientUserId = user.Id,
                PatientProfileId = patient.Id,
                SelectedDoctorId = picked.DoctorId,
                SelectedLocalIso = picked.LocalIso,
                SelectedScheduledUtc = scheduledUtc,
                Reason = Input.Reason
            };

            var payload = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });

            var folder = GetPrivateRequestsFolder(patient.Id);
            Directory.CreateDirectory(folder);

            var safeName = $"appointment_request_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.json";
            var fullPath = Path.Combine(folder, safeName);
            await System.IO.File.WriteAllTextAsync(fullPath, payload);

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = null,
                FileName = safeName,
                FilePath = fullPath,
                ContentType = "application/json",
                Type = "AppointmentRequest",
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Pending,
                PatientNotes = Input.Reason,
                ValidationNotes = $"Selected:{picked.LocalIso}"
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            var assistants = await _userManager.GetUsersInRoleAsync("Assistant");
            foreach (var a in assistants)
            {
                if (!string.IsNullOrWhiteSpace(user.ClinicId) && a.ClinicId != user.ClinicId)
                    continue;

                await _notifier.NotifyAsync(
                    a,
                    NotificationType.Appointment,
                    "New appointment request",
                    $"A patient requested a clinic appointment.<br/>Requested: <b>{FormatLocalIsoForDisplay(picked.LocalIso)}</b>",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString(),
                    sendEmail: false
                );
            }

            await _notifier.NotifyAsync(
                user,
                NotificationType.Appointment,
                "Appointment request submitted",
                $"Your request for <b>{FormatLocalIsoForDisplay(picked.LocalIso)}</b> was sent to the clinic and is waiting for review.",
                relatedEntity: "MedicalAttachment",
                relatedEntityId: attachment.Id.ToString(),
                sendEmail: false
            );

            TempData["StatusMessage"] = "Request sent. You will be notified after it is reviewed.";
            return RedirectToPage("/Patient/Appointments/Index");
        }

        private async Task LoadSlotsAsync()
        {
            AvailableSlots = new();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            var clinicId = user.ClinicId;

            var doctorRoleId = await _db.Roles
                .AsNoTracking()
                .Where(r => r.Name == "Doctor")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var doctorUserIds = new List<string>();

            if (!string.IsNullOrWhiteSpace(doctorRoleId))
            {
                doctorUserIds = await _db.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.RoleId == doctorRoleId)
                    .Select(ur => ur.UserId)
                    .ToListAsync();
            }

            var doctors = await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Where(d => d.User != null)
                .Where(d => doctorUserIds.Contains(d.UserId))
                .Where(d => string.IsNullOrWhiteSpace(clinicId) || (d.User != null && d.User.ClinicId == clinicId))
                .ToListAsync();

            if (doctors.Count == 0)
                return;

            foreach (var d in doctors)
            {
                var schedule = await _db.DoctorAvailabilities
                    .AsNoTracking()
                    .Where(x => x.DoctorId == d.Id && x.IsActive)
                    .ToListAsync();

                if (schedule.Count == 0)
                    continue;

                var slots = await BuildSlotsAsync(d.Id, schedule);

                foreach (var s in slots)
                {
                    AvailableSlots.Add(new SlotOptionVm
                    {
                        DoctorId = d.Id,
                        LocalIso = s,
                        Key = $"{d.Id:N}|{s}",
                        Display = $"Clinic — {FormatLocalIsoForDisplay(s)}"
                    });
                }
            }

            AvailableSlots = AvailableSlots
                .OrderBy(x => x.LocalIso, StringComparer.Ordinal)
                .Take(250)
                .ToList();
        }

        private async Task<List<string>> BuildSlotsAsync(Guid doctorId, List<DoctorAvailability> schedule)
        {
            var result = new List<string>();
            if (schedule == null || schedule.Count == 0)
                return result;

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(DaysAhead);

            var rangeStartUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Local).ToUniversalTime();
            var rangeEndUtc = DateTime.SpecifyKind(endDate.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var existingAppointments = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.Rejected
                            && a.ScheduledAt >= rangeStartUtc
                            && a.ScheduledAt < rangeEndUtc)
                .ToListAsync();

            var proposedStartsUtc = await GetProposedStartsUtcAsync(doctorId, rangeStartUtc, rangeEndUtc);

            for (var day = startDate; day <= endDate; day = day.AddDays(1))
            {
                var daySlots = schedule
                    .Where(s => s.IsActive && s.DayOfWeek == day.DayOfWeek)
                    .ToList();

                if (daySlots.Count == 0)
                    continue;

                foreach (var s in daySlots)
                {
                    for (var t = s.StartTime; t < s.EndTime; t = t.Add(TimeSpan.FromMinutes(SlotMinutes)))
                    {
                        var local = DateTime.SpecifyKind(day.Date + t, DateTimeKind.Local);
                        var utc = local.ToUniversalTime();

                        if (utc <= DateTime.UtcNow)
                            continue;

                        var newStartUtc = utc;
                        var newEndUtc = utc.AddMinutes(DurationMinutes);

                        var apptOverlap = existingAppointments.Any(a =>
                            a.ScheduledAt < newEndUtc &&
                            a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc);

                        if (apptOverlap)
                            continue;

                        var proposedOverlap = proposedStartsUtc.Any(p =>
                            p < newEndUtc &&
                            p.AddMinutes(DurationMinutes) > newStartUtc);

                        if (proposedOverlap)
                            continue;

                        result.Add(local.ToString("yyyy-MM-ddTHH:mm"));
                    }
                }
            }

            return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        private async Task<bool> ValidateSlotStillAvailableAsync(Guid doctorId, DateTime scheduledLocalKinded)
        {
            var day = scheduledLocalKinded.DayOfWeek;
            var time = scheduledLocalKinded.TimeOfDay;

            var daySlots = await _db.DoctorAvailabilities
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.IsActive && a.DayOfWeek == day)
                .ToListAsync();

            if (!daySlots.Any() || !daySlots.Any(s => time >= s.StartTime && time < s.EndTime))
                return false;

            if (time.Minutes % SlotMinutes != 0)
                return false;

            var scheduledUtc = scheduledLocalKinded.ToUniversalTime();
            if (scheduledUtc <= DateTime.UtcNow)
                return false;

            var newStartUtc = scheduledUtc;
            var newEndUtc = scheduledUtc.AddMinutes(DurationMinutes);

            var doctorConflict = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.Rejected
                            && a.ScheduledAt < newEndUtc
                            && a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc)
                .AnyAsync();

            if (doctorConflict)
                return false;

            var user = await _userManager.GetUserAsync(User);
            var patient = user == null ? null : await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient != null)
            {
                var patientConflict = await _db.Appointments
                    .AsNoTracking()
                    .Where(a => a.PatientId == patient.Id
                                && a.Status != AppointmentStatus.Cancelled
                                && a.Status != AppointmentStatus.Rejected
                                && a.ScheduledAt < newEndUtc
                                && a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc)
                    .AnyAsync();

                if (patientConflict)
                    return false;
            }

            var proposedStartsUtc = await GetProposedStartsUtcAsync(doctorId, newStartUtc.AddDays(-1), newEndUtc.AddDays(1));
            var proposedOverlap = proposedStartsUtc.Any(p =>
                p < newEndUtc &&
                p.AddMinutes(DurationMinutes) > newStartUtc);

            return !proposedOverlap;
        }

        private async Task<List<DateTime>> GetProposedStartsUtcAsync(Guid doctorId, DateTime rangeStartUtc, DateTime rangeEndUtc)
        {
            var items = await _db.MedicalAttachments
                .AsNoTracking()
                .Where(a => a.Type == "AppointmentRequest"
                            && a.Status == AttachmentStatus.Pending
                            && a.DoctorId == doctorId
                            && a.ValidationNotes != null
                            && a.ValidationNotes.Contains("Suggested:"))
                .Select(a => a.ValidationNotes!)
                .ToListAsync();

            var result = new List<DateTime>();

            foreach (var notes in items)
            {
                var iso = ExtractSuggestedIso(notes);
                if (string.IsNullOrWhiteSpace(iso))
                    continue;

                if (!DateTime.TryParseExact(iso, new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    continue;

                var local = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                var utc = local.ToUniversalTime();

                if (utc >= rangeStartUtc && utc < rangeEndUtc)
                    result.Add(utc);
            }

            return result;
        }

        private string GetPrivateRequestsFolder(Guid patientId)
        {
            return Path.Combine(_env.ContentRootPath, "Files", "uploads", "patient", patientId.ToString(), "requests");
        }

        private static string ExtractSuggestedIso(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";
            var idx = notes.IndexOf("Suggested:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            var v = notes[(idx + "Suggested:".Length)..].Trim();
            if (v.Contains("|"))
                v = v.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return string.IsNullOrWhiteSpace(v) ? "" : v;
        }

        private static string FormatLocalIsoForDisplay(string localIso)
        {
            if (!DateTime.TryParseExact(localIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return localIso;

            var local = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return local.ToString("ddd dd MMM HH:mm");
        }
    }
}

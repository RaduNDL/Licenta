using System;
using System.Collections.Generic;
using System.Globalization;
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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class ProcessModel : PageModel
    {
        private const int SlotMinutes = 30;
        private const int DurationMinutes = 30;
        private const int DaysAhead = 14;

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public ProcessModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
            _notifier = notifier;
        }

        public class SlotVm
        {
            public string LocalIso { get; set; } = "";
            public string Display { get; set; } = "";
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

        [BindProperty(SupportsGet = true)]
        public Guid AttachmentId { get; set; }

        [BindProperty]
        public Guid SelectedDoctorId { get; set; }

        [BindProperty]
        public string SelectedLocalIso { get; set; } = "";

        public SelectList Doctors { get; set; } = default!;
        public List<SlotVm> AvailableSlots { get; set; } = new();

        public string PatientName { get; set; } = "";
        public string PreferredDisplay { get; set; } = "";
        public string Reason { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(Guid? attachmentId, Guid? doctorId)
        {
            if (attachmentId.HasValue)
                AttachmentId = attachmentId.Value;

            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == AttachmentId && a.Type == "AppointmentRequest");

            if (att == null)
                return NotFound();

            if (att.Status != AttachmentStatus.Pending)
                return RedirectToPage("/Assistant/Appointments/Requests");

            if (IsAwaitingDoctorApproval(att.ValidationNotes))
                return RedirectToPage("/Assistant/Appointments/Requests");

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                var patientClinic = att.Patient?.User?.ClinicId;
                if (patientClinic != assistant.ClinicId)
                    return Forbid();
            }

            PatientName = att.Patient?.User?.FullName ?? att.Patient?.User?.Email ?? "Unknown patient";

            var payload = await TryReadPayloadAsync(att.FilePath);

            Reason = !string.IsNullOrWhiteSpace(payload?.Reason)
                ? payload!.Reason
                : (string.IsNullOrWhiteSpace(att.PatientNotes) ? "-" : att.PatientNotes);

            PreferredDisplay = !string.IsNullOrWhiteSpace(payload?.SelectedLocalIso)
                ? FormatLocalIsoForDisplay(payload!.SelectedLocalIso)
                : ExtractRequestedDisplay(att.ValidationNotes);

            var preferredDoctor = doctorId.HasValue && doctorId.Value != Guid.Empty
                ? doctorId.Value
                : (payload != null && payload.SelectedDoctorId != Guid.Empty ? payload.SelectedDoctorId : (Guid?)null);

            await LoadDoctorsAsync(assistant.ClinicId, preferredDoctor);

            if (SelectedDoctorId != Guid.Empty)
            {
                var schedule = await _db.DoctorAvailabilities.AsNoTracking()
                    .Where(x => x.DoctorId == SelectedDoctorId && x.IsActive)
                    .ToListAsync();

                AvailableSlots = await BuildSlotsAsync(SelectedDoctorId, schedule);

                if (!string.IsNullOrWhiteSpace(payload?.SelectedLocalIso) && AvailableSlots.Exists(s => s.LocalIso == payload.SelectedLocalIso))
                    SelectedLocalIso = payload.SelectedLocalIso;
                else if (AvailableSlots.Count > 0)
                    SelectedLocalIso = AvailableSlots[0].LocalIso;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostScheduleNowAsync()
        {
            return await HandleAsync(createImmediately: true);
        }

        public async Task<IActionResult> OnPostSendToDoctorAsync()
        {
            return await HandleAsync(createImmediately: false);
        }

        private async Task<IActionResult> HandleAsync(bool createImmediately)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == AttachmentId && a.Type == "AppointmentRequest");

            if (att == null)
                return NotFound();

            if (att.Status != AttachmentStatus.Pending)
                return RedirectToPage("/Assistant/Appointments/Requests");

            if (IsAwaitingDoctorApproval(att.ValidationNotes))
                return RedirectToPage("/Assistant/Appointments/Requests");

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                var patientClinic = att.Patient?.User?.ClinicId;
                if (patientClinic != assistant.ClinicId)
                    return Forbid();
            }

            if (SelectedDoctorId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(SelectedDoctorId), "Please select a doctor.");
                await LoadDoctorsAsync(assistant.ClinicId, null);
                return Page();
            }

            if (!DateTime.TryParseExact(SelectedLocalIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledLocal))
            {
                ModelState.AddModelError(nameof(SelectedLocalIso), "Invalid slot.");
                await LoadDoctorsAsync(assistant.ClinicId, SelectedDoctorId);
                return Page();
            }

            var scheduledLocalKinded = DateTime.SpecifyKind(scheduledLocal, DateTimeKind.Local);
            var scheduledUtc = scheduledLocalKinded.ToUniversalTime();

            if (scheduledUtc <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(SelectedLocalIso), "You cannot schedule in the past.");
                await LoadDoctorsAsync(assistant.ClinicId, SelectedDoctorId);
                return Page();
            }

            var doctorEntity = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == SelectedDoctorId);

            if (doctorEntity == null)
            {
                ModelState.AddModelError(nameof(SelectedDoctorId), "Selected doctor not found.");
                await LoadDoctorsAsync(assistant.ClinicId, null);
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId) && doctorEntity.User != null && doctorEntity.User.ClinicId != assistant.ClinicId)
                return Forbid();

            var day = scheduledLocalKinded.DayOfWeek;
            var time = scheduledLocalKinded.TimeOfDay;

            var daySlots = await _db.DoctorAvailabilities.AsNoTracking()
                .Where(a => a.DoctorId == SelectedDoctorId && a.IsActive && a.DayOfWeek == day)
                .ToListAsync();

            if (!daySlots.Any() || !daySlots.Any(s => time >= s.StartTime && time < s.EndTime))
            {
                ModelState.AddModelError(nameof(SelectedLocalIso), "Selected time is outside doctor's working hours.");
                await LoadDoctorsAsync(assistant.ClinicId, SelectedDoctorId);
                return Page();
            }

            if (time.Minutes % SlotMinutes != 0)
            {
                ModelState.AddModelError(nameof(SelectedLocalIso), "Selected slot is not aligned to the slot step.");
                await LoadDoctorsAsync(assistant.ClinicId, SelectedDoctorId);
                return Page();
            }

            var newStartUtc = scheduledUtc;
            var newEndUtc = scheduledUtc.AddMinutes(DurationMinutes);

            var doctorConflict = await _db.Appointments.AsNoTracking()
                .AnyAsync(a => a.DoctorId == SelectedDoctorId
                              && a.Status != AppointmentStatus.Cancelled
                              && a.Status != AppointmentStatus.Rejected
                              && a.ScheduledAt < newEndUtc
                              && a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc);

            if (doctorConflict)
            {
                ModelState.AddModelError(nameof(SelectedLocalIso), "Selected slot is no longer available.");
                await LoadDoctorsAsync(assistant.ClinicId, SelectedDoctorId);
                return Page();
            }

            var patientConflict = await _db.Appointments.AsNoTracking()
                .AnyAsync(a => a.PatientId == att.PatientId
                              && a.Status != AppointmentStatus.Cancelled
                              && a.Status != AppointmentStatus.Rejected
                              && a.ScheduledAt < newEndUtc
                              && a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc);

            if (patientConflict)
            {
                ModelState.AddModelError(nameof(SelectedLocalIso), "Patient already has an appointment around that time.");
                await LoadDoctorsAsync(assistant.ClinicId, SelectedDoctorId);
                return Page();
            }

            var payload = await TryReadPayloadAsync(att.FilePath);
            var reason = !string.IsNullOrWhiteSpace(payload?.Reason) ? payload!.Reason : (att.PatientNotes ?? "Appointment");

            if (createImmediately)
            {
                var appointment = new Appointment
                {
                    PatientId = att.PatientId,
                    DoctorId = SelectedDoctorId,
                    ScheduledAt = scheduledUtc,
                    Status = AppointmentStatus.Approved,
                    Reason = reason,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    Location = "Clinic",
                    StartTimeUtc = scheduledUtc
                };

                _db.Appointments.Add(appointment);

                att.DoctorId = SelectedDoctorId;
                att.Status = AttachmentStatus.Validated;
                att.ValidatedAtUtc = DateTime.UtcNow;
                att.ValidationNotes = $"SCHEDULED_BY_ASSISTANT|Scheduled:{scheduledLocalKinded:yyyy-MM-ddTHH:mm}|{att.ValidationNotes ?? ""}";

                await _db.SaveChangesAsync();

                var patientUser = att.Patient?.User;
                var doctorUser = doctorEntity.User;

                if (patientUser != null)
                {
                    await _notifier.NotifyAsync(
                        patientUser,
                        NotificationType.Appointment,
                        "Appointment scheduled",
                        $"Your clinic appointment was scheduled for: <b>{appointment.ScheduledAt.ToLocalTime():f}</b>.",
                        relatedEntity: "Appointment",
                        relatedEntityId: appointment.Id.ToString(),
                        sendEmail: false
                    );
                }

                if (doctorUser != null)
                {
                    await _notifier.NotifyAsync(
                        doctorUser,
                        NotificationType.Appointment,
                        "New appointment scheduled",
                        $"New appointment for patient <b>{(patientUser?.FullName ?? patientUser?.Email)}</b><br/>When: {appointment.ScheduledAt.ToLocalTime():f}<br/>Reason: {appointment.Reason}",
                        relatedEntity: "Appointment",
                        relatedEntityId: appointment.Id.ToString(),
                        sendEmail: false
                    );
                }

                TempData["StatusMessage"] = "Scheduled successfully (A).";
                return RedirectToPage("/Assistant/Appointments/Requests");
            }

            att.DoctorId = SelectedDoctorId;
            att.ValidationNotes = $"AWAITING_DOCTOR_APPROVAL|Suggested:{scheduledLocalKinded:yyyy-MM-ddTHH:mm}|{att.ValidationNotes ?? ""}";
            await _db.SaveChangesAsync();

            var doctorForApproval = doctorEntity.User;

            if (doctorForApproval != null)
            {
                await _notifier.NotifyAsync(
                    doctorForApproval,
                    NotificationType.Appointment,
                    "Appointment request needs your approval",
                    $"A clinic appointment request was forwarded for your approval.<br/>Suggested: <b>{scheduledLocalKinded:f}</b><br/>Patient: <b>{att.Patient?.User?.FullName ?? att.Patient?.User?.Email}</b>",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: att.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Sent to doctor for approval (B).";
            return RedirectToPage("/Assistant/Appointments/Requests");
        }

        private async Task LoadDoctorsAsync(string? clinicId, Guid? preferredDoctorId)
        {
            var doctorsQuery = _db.Doctors.Include(d => d.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(clinicId))
                doctorsQuery = doctorsQuery.Where(d => d.User.ClinicId == clinicId);

            var list = await doctorsQuery
                .OrderBy(d => d.User.FullName ?? d.User.Email)
                .Select(d => new { d.Id, Name = d.User.FullName ?? d.User.Email ?? "(no name)" })
                .ToListAsync();

            var effective = preferredDoctorId.HasValue && preferredDoctorId.Value != Guid.Empty
                ? preferredDoctorId.Value
                : (list.Count > 0 ? list[0].Id : Guid.Empty);

            SelectedDoctorId = effective;

            Doctors = new SelectList(list, "Id", "Name", effective);
        }

        private async Task<AppointmentRequestPayload?> TryReadPayloadAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            try
            {
                if (!System.IO.File.Exists(filePath))
                    return null;

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<AppointmentRequestPayload>(json);
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<SlotVm>> BuildSlotsAsync(Guid doctorId, List<DoctorAvailability> schedule)
        {
            var result = new List<SlotVm>();
            if (schedule == null || schedule.Count == 0)
                return result;

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(DaysAhead);

            var rangeStartUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Local).ToUniversalTime();
            var rangeEndUtc = DateTime.SpecifyKind(endDate.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var existing = await _db.Appointments.AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.Rejected
                            && a.ScheduledAt >= rangeStartUtc
                            && a.ScheduledAt < rangeEndUtc)
                .ToListAsync();

            for (var day = startDate; day <= endDate; day = day.AddDays(1))
            {
                var daySlots = schedule.Where(s => s.IsActive && s.DayOfWeek == day.DayOfWeek).ToList();
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

                        var overlap = existing.Any(a =>
                            a.ScheduledAt < newEndUtc &&
                            a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc);

                        if (overlap)
                            continue;

                        result.Add(new SlotVm
                        {
                            LocalIso = local.ToString("yyyy-MM-ddTHH:mm"),
                            Display = local.ToString("ddd dd MMM HH:mm")
                        });
                    }
                }
            }

            return result.OrderBy(x => x.LocalIso, StringComparer.Ordinal).ToList();
        }

        private static bool IsAwaitingDoctorApproval(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return false;
            return notes.IndexOf("AWAITING_DOCTOR_APPROVAL", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractRequestedDisplay(string? notes)
        {
            var iso = ExtractRequestedIso(notes);
            if (string.IsNullOrWhiteSpace(iso))
                return "-";

            if (!DateTime.TryParseExact(iso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return iso;

            var local = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return local.ToString("ddd dd MMM HH:mm");
        }

        private static string ExtractRequestedIso(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";
            var idx = notes.IndexOf("Selected:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            var v = notes[(idx + "Selected:".Length)..].Trim();
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

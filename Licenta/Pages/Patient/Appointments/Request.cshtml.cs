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

        public class SlotUiModel
        {
            public string Key { get; set; } = "";
            public string TimeDisplay { get; set; } = "";
            public string LocalIso { get; set; } = "";
        }

        public class DoctorUiModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string Specialty { get; set; } = "";
            public string ProfileImagePath { get; set; } = "";
            public Dictionary<string, List<SlotUiModel>> DaysAndSlots { get; set; } = new();
        }

        public string DoctorsJson { get; set; } = "[]";

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Please select a time slot.")]
            public string SelectedSlotKey { get; set; } = "";

            [Required(ErrorMessage = "Please provide a reason for the visit.")]
            [MaxLength(2000)]
            public string Reason { get; set; } = "";
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
            if (user == null) return Page();

            var parts = Input.SelectedSlotKey.Split('|');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var doctorId))
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "Invalid slot selected.");
                return Page();
            }
            var localIso = parts[1];

            if (!DateTime.TryParseExact(localIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDt))
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "Invalid slot time.");
                return Page();
            }

            var localKinded = DateTime.SpecifyKind(localDt, DateTimeKind.Local);
            var scheduledUtc = localKinded.ToUniversalTime();

            if (scheduledUtc <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(Input.SelectedSlotKey), "You cannot choose a past slot.");
                return Page();
            }

            var ok = await ValidateSlotStillAvailableAsync(doctorId, localKinded);
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
                SelectedDoctorId = doctorId,
                SelectedLocalIso = localIso,
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
                DoctorId = doctorId,
                FileName = safeName,
                FilePath = fullPath,
                ContentType = "application/json",
                Type = "AppointmentRequest",
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Pending,
                PatientNotes = Input.Reason,
                ValidationNotes = $"Selected:{localIso}"
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            var doctorUser = await _db.Users.FirstOrDefaultAsync(u => _db.Doctors.Any(d => d.Id == doctorId && d.UserId == u.Id));

            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(
                    doctorUser,
                    NotificationType.Appointment,
                    "New Appointment Request",
                    $"A patient requested an appointment for <b>{FormatLocalIsoForDisplay(localIso)}</b>.",
                    actionUrl: "/Doctor/Attachments/Inbox",
                    actionText: "Open Inbox",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString(),
                    sendEmail: false
                );
            }

            await _notifier.NotifyAsync(
                user,
                NotificationType.Appointment,
                "Appointment request submitted",
                $"Your request for <b>{FormatLocalIsoForDisplay(localIso)}</b> was sent to Dr. {doctorUser?.FullName}.",
                actionUrl: "/Patient/Appointments/Index",
                actionText: "View Appointments",
                relatedEntity: "MedicalAttachment",
                relatedEntityId: attachment.Id.ToString(),
                sendEmail: false
            );

            TempData["StatusMessage"] = "Request sent. You will be notified after it is reviewed by the doctor.";
            return RedirectToPage("/Patient/Appointments/Index");
        }

        private async Task LoadSlotsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var clinicId = user.ClinicId;
            var doctorRoleId = await _db.Roles.AsNoTracking().Where(r => r.Name == "Doctor").Select(r => r.Id).FirstOrDefaultAsync();
            var doctorUserIds = new List<string>();

            if (!string.IsNullOrWhiteSpace(doctorRoleId))
            {
                doctorUserIds = await _db.UserRoles.AsNoTracking().Where(ur => ur.RoleId == doctorRoleId).Select(ur => ur.UserId).ToListAsync();
            }

            var doctors = await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Where(d => d.User != null && doctorUserIds.Contains(d.UserId))
                .Where(d => string.IsNullOrWhiteSpace(clinicId) || d.User!.ClinicId == clinicId)
                .ToListAsync();

            var doctorsUiList = new List<DoctorUiModel>();

            foreach (var d in doctors)
            {
                var schedule = await _db.DoctorAvailabilities.AsNoTracking().Where(x => x.DoctorId == d.Id && x.IsActive).ToListAsync();
                var slots = new List<string>();

                if (schedule.Count > 0)
                {
                    slots = await BuildSlotsAsync(d.Id, schedule);
                }

                var docUi = new DoctorUiModel
                {
                    Id = d.Id,
                    Name = d.User?.FullName ?? "Doctor",
                    Specialty = d.Specialty ?? "General Practice",
                    ProfileImagePath = string.IsNullOrWhiteSpace(d.ProfileImagePath) ? "/images/default.jpg" : d.ProfileImagePath
                };

                if (slots.Count > 0)
                {
                    var grouped = slots.GroupBy(s => DateTime.Parse(s).ToString("yyyy-MM-dd"));
                    foreach (var g in grouped)
                    {
                        docUi.DaysAndSlots[g.Key] = g.Select(s => new SlotUiModel
                        {
                            Key = $"{d.Id:N}|{s}",
                            TimeDisplay = DateTime.Parse(s).ToString("HH:mm"),
                            LocalIso = s
                        }).ToList();
                    }
                }

                doctorsUiList.Add(docUi);
            }

            DoctorsJson = JsonSerializer.Serialize(doctorsUiList);
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

        private async Task<bool> ValidateSlotStillAvailableAsync(Guid doctorId, DateTime scheduledLocalKinded)
        {
            var day = scheduledLocalKinded.DayOfWeek;
            var time = scheduledLocalKinded.TimeOfDay;

            var daySlots = await _db.DoctorAvailabilities.AsNoTracking().Where(a => a.DoctorId == doctorId && a.IsActive && a.DayOfWeek == day).ToListAsync();
            if (!daySlots.Any() || !daySlots.Any(s => time >= s.StartTime && time < s.EndTime)) return false;
            if (time.Minutes % SlotMinutes != 0) return false;

            var scheduledUtc = scheduledLocalKinded.ToUniversalTime();
            if (scheduledUtc <= DateTime.UtcNow) return false;

            var doctorConflict = await _db.Appointments.AsNoTracking().AnyAsync(a => a.DoctorId == doctorId && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected && a.ScheduledAt < scheduledUtc.AddMinutes(DurationMinutes) && a.ScheduledAt.AddMinutes(DurationMinutes) > scheduledUtc);
            if (doctorConflict) return false;

            var proposedStartsUtc = await GetProposedStartsUtcAsync(doctorId, scheduledUtc.AddDays(-1), scheduledUtc.AddMinutes(DurationMinutes).AddDays(1));
            return !proposedStartsUtc.Any(p => p < scheduledUtc.AddMinutes(DurationMinutes) && p.AddMinutes(DurationMinutes) > scheduledUtc);
        }

        private async Task<List<DateTime>> GetProposedStartsUtcAsync(Guid doctorId, DateTime rangeStartUtc, DateTime rangeEndUtc)
        {
            var items = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => a.Type == "AppointmentRequest" && a.Status == AttachmentStatus.Pending && a.DoctorId == doctorId && a.ValidationNotes != null)
                .Select(a => a.ValidationNotes!).ToListAsync();

            var result = new List<DateTime>();
            foreach (var notes in items)
            {
                var iso = ExtractRequestedIso(notes);
                if (!string.IsNullOrWhiteSpace(iso) && DateTime.TryParseExact(iso, new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    var utc = DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
                    if (utc >= rangeStartUtc && utc < rangeEndUtc) result.Add(utc);
                }
            }
            return result;
        }

        private string GetPrivateRequestsFolder(Guid patientId) => Path.Combine(_env.ContentRootPath, "Files", "uploads", "patient", patientId.ToString(), "requests");

        private static string ExtractRequestedIso(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";
            var idx = notes.IndexOf("Selected:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            var v = notes[(idx + "Selected:".Length)..].Trim();
            if (v.Contains("|")) v = v.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return string.IsNullOrWhiteSpace(v) ? "" : v;
        }

        private static string FormatLocalIsoForDisplay(string localIso)
        {
            if (!DateTime.TryParseExact(localIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return localIso;
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToString("ddd dd MMM HH:mm");
        }
    }
}
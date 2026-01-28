using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Appointments
{
    [Authorize(Roles = "Doctor")]
    public class ApprovalDetailsModel : PageModel
    {
        private const int DurationMinutes = 30;

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public ApprovalDetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        [BindProperty(SupportsGet = true)]
        public Guid AttachmentId { get; set; }

        public DetailsVm? Item { get; set; }

        public class DetailsVm
        {
            public Guid AttachmentId { get; set; }
            public string PatientName { get; set; } = "";
            public string Reason { get; set; } = "";
            public string RequestedDisplay { get; set; } = "-";
            public string SuggestedDisplay { get; set; } = "";
            public string UploadedAtLocal { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            Item = await LoadVmAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Doctor/Appointments/Approvals");

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return RedirectToPage("/Doctor/Appointments/Approvals");

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == AttachmentId &&
                    a.Type == "AppointmentRequest" &&
                    a.DoctorId == doctor.Id);

            if (att == null)
            {
                TempData["StatusMessage"] = "Item not found.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            if (att.Status != AttachmentStatus.Pending || !IsAwaitingDoctorApproval(att.ValidationNotes))
            {
                TempData["StatusMessage"] = "This request was already processed or is not awaiting doctor approval.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var iso = ExtractSuggestedIso(att.ValidationNotes);
            if (string.IsNullOrWhiteSpace(iso) || !TryParseLocal(iso, out var local))
            {
                TempData["StatusMessage"] = "Cannot approve: suggested time is missing or invalid.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var newUtc = local.ToUniversalTime();
            if (newUtc <= DateTime.UtcNow)
            {
                TempData["StatusMessage"] = "Cannot approve: suggested time is in the past.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            if (att.PatientId == Guid.Empty)
            {
                TempData["StatusMessage"] = "Cannot approve: patient is missing.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var withinHours = await IsWithinDoctorAvailabilityAsync(doctor.Id, newUtc);
            if (!withinHours)
            {
                TempData["StatusMessage"] = "Cannot approve: suggested time is outside your working hours.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var newStartUtc = newUtc;
            var newEndUtc = newUtc.AddMinutes(DurationMinutes);

            var doctorConflict = await _db.Appointments.AsNoTracking().AnyAsync(a =>
                a.DoctorId == doctor.Id &&
                a.Status != AppointmentStatus.Cancelled &&
                a.Status != AppointmentStatus.Rejected &&
                a.ScheduledAt < newEndUtc &&
                a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc);

            if (doctorConflict)
            {
                TempData["StatusMessage"] = "Cannot approve: you already have an appointment around that time.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var patientConflict = await _db.Appointments.AsNoTracking().AnyAsync(a =>
                a.PatientId == att.PatientId &&
                a.Status != AppointmentStatus.Cancelled &&
                a.Status != AppointmentStatus.Rejected &&
                a.ScheduledAt < newEndUtc &&
                a.ScheduledAt.AddMinutes(DurationMinutes) > newStartUtc);

            if (patientConflict)
            {
                TempData["StatusMessage"] = "Cannot approve: the patient already has an appointment around that time.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var appt = new Appointment
            {
                PatientId = att.PatientId,
                DoctorId = doctor.Id,
                ScheduledAt = newUtc,
                Reason = string.IsNullOrWhiteSpace(att.PatientNotes) ? null : att.PatientNotes,
                Status = AppointmentStatus.Approved,
                Location = "Clinic",
                StartTimeUtc = newUtc,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Appointments.Add(appt);

            att.Status = AttachmentStatus.Validated;
            att.ValidatedAtUtc = DateTime.UtcNow;
            att.ValidatedByDoctorId = doctor.Id;
            att.DoctorNotes = "Approved by doctor.";
            att.ValidationNotes = $"APPROVED_BY_DOCTOR|Scheduled:{local:yyyy-MM-ddTHH:mm}|{att.ValidationNotes ?? ""}";

            await _db.SaveChangesAsync();

            var patientUser = att.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Appointment Confirmed",
                    $"Your appointment was approved and scheduled for <b>{local:ddd dd MMM HH:mm}</b>.",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            if (doctor.User != null)
            {
                await _notifier.NotifyAsync(
                    doctor.User,
                    NotificationType.Appointment,
                    "Appointment approved",
                    $"You approved an appointment for <b>{(patientUser?.FullName ?? patientUser?.Email ?? "patient")}</b><br/>When: <b>{local:ddd dd MMM HH:mm}</b>",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Approved and appointment created.";
            return RedirectToPage("/Doctor/Appointments/Approvals");
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Doctor/Appointments/Approvals");

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return RedirectToPage("/Doctor/Appointments/Approvals");

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == AttachmentId &&
                    a.Type == "AppointmentRequest" &&
                    a.DoctorId == doctor.Id);

            if (att == null)
            {
                TempData["StatusMessage"] = "Item not found.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            if (att.Status != AttachmentStatus.Pending || !IsAwaitingDoctorApproval(att.ValidationNotes))
            {
                TempData["StatusMessage"] = "This request was already processed or is not awaiting doctor approval.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            att.Status = AttachmentStatus.Rejected;
            att.ValidatedAtUtc = DateTime.UtcNow;
            att.ValidatedByDoctorId = doctor.Id;
            att.DoctorNotes = "Rejected by doctor.";
            att.ValidationNotes = $"REJECTED_BY_DOCTOR|{att.ValidationNotes ?? ""}";

            await _db.SaveChangesAsync();

            var patientUser = att.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Appointment Rejected",
                    "Your appointment request was rejected by the doctor. Please submit a new request.",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: att.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Rejected.";
            return RedirectToPage("/Doctor/Appointments/Approvals");
        }

        private async Task<DetailsVm?> LoadVmAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return null;

            var att = await _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == AttachmentId &&
                    a.Type == "AppointmentRequest" &&
                    a.DoctorId == doctor.Id);

            if (att == null) return null;

            return new DetailsVm
            {
                AttachmentId = att.Id,
                PatientName = att.Patient?.User?.FullName ?? att.Patient?.User?.Email ?? "Patient",
                Reason = string.IsNullOrWhiteSpace(att.PatientNotes) ? "-" : att.PatientNotes!,
                RequestedDisplay = ExtractRequestedDisplay(att.ValidationNotes),
                SuggestedDisplay = ExtractSuggestedDisplay(att.ValidationNotes),
                UploadedAtLocal = att.UploadedAt.ToLocalTime().ToString("g")
            };
        }

        private async Task<bool> IsWithinDoctorAvailabilityAsync(Guid doctorId, DateTime scheduledUtc)
        {
            var local = scheduledUtc.ToLocalTime();
            var day = local.DayOfWeek;
            var minutes = local.Hour * 60 + local.Minute;

            var items = await _db.DoctorAvailabilities
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.IsActive)
                .ToListAsync();

            if (items.Count == 0) return true;

            foreach (var a in items)
            {
                if (a.DayOfWeek != day) continue;

                var start = a.StartTime.Hours * 60 + a.StartTime.Minutes;
                var end = a.EndTime.Hours * 60 + a.EndTime.Minutes;

                if (minutes >= start && minutes < end)
                    return true;
            }

            return false;
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

            if (TryParseLocal(iso, out var local))
                return local.ToString("ddd dd MMM HH:mm");

            return iso;
        }

        private static string ExtractSuggestedDisplay(string? notes)
        {
            var iso = ExtractSuggestedIso(notes);
            if (string.IsNullOrWhiteSpace(iso))
                return "-";

            if (TryParseLocal(iso, out var local))
                return local.ToString("ddd dd MMM HH:mm");

            return iso;
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

        private static bool TryParseLocal(string value, out DateTime local)
        {
            local = default;

            var formats = new[]
            {
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss"
            };

            if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return false;

            local = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return true;
        }
    }
}

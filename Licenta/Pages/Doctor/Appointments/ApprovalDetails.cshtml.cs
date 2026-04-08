using System;
using System.ComponentModel.DataAnnotations;
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
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public ApprovalDetailsModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        [BindProperty(SupportsGet = true)]
        public Guid AttachmentId { get; set; }

        [BindProperty]
        [StringLength(200)]
        public string? DecisionNote { get; set; }

        [BindProperty]
        [StringLength(200)]
        public string Location { get; set; } = "Clinic";

        public bool CanView { get; set; }
        public string PatientName { get; set; } = "Patient";
        public string DoctorName { get; set; } = "Doctor";
        public string UploadedAtDisplay { get; set; } = "-";
        public string RequestedSlotDisplay { get; set; } = "-";
        public string Reason { get; set; } = "-";

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            CanView = false;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            var doctor = await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
                return;

            var attachment = await _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == AttachmentId &&
                    a.Type == "AppointmentRequest" &&
                    a.DoctorId == doctor.Id);

            if (attachment == null)
                return;

            CanView = true;
            PatientName = attachment.Patient?.User?.FullName ?? attachment.Patient?.User?.Email ?? "Patient";
            DoctorName = attachment.Doctor?.User?.FullName ?? attachment.Doctor?.User?.Email ?? "Doctor";
            UploadedAtDisplay = attachment.UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            RequestedSlotDisplay = FormatLocalIsoForDisplay(ExtractRequestedIso(attachment.ValidationNotes));
            Reason = string.IsNullOrWhiteSpace(attachment.PatientNotes) ? "-" : attachment.PatientNotes;
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
                return Forbid();

            var attachment = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == AttachmentId &&
                    a.Type == "AppointmentRequest" &&
                    a.DoctorId == doctor.Id);

            if (attachment == null)
                return NotFound();

            if (attachment.Status != AttachmentStatus.Pending)
            {
                TempData["StatusMessage"] = "This request has already been processed.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var requestedIso = ExtractRequestedIso(attachment.ValidationNotes);
            if (string.IsNullOrWhiteSpace(requestedIso) ||
                !DateTime.TryParseExact(
                    requestedIso,
                    new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var localDt))
            {
                TempData["StatusMessage"] = "Invalid requested slot.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var localKinded = DateTime.SpecifyKind(localDt, DateTimeKind.Local);
            var scheduledUtc = localKinded.ToUniversalTime();

            if (scheduledUtc <= DateTime.UtcNow)
            {
                TempData["StatusMessage"] = "The selected slot is in the past.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var conflict = await _db.Appointments
                .AnyAsync(a =>
                    a.DoctorId == doctor.Id &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.Rejected &&
                    a.ScheduledAt < scheduledUtc.AddMinutes(30) &&
                    a.ScheduledAt.AddMinutes(30) > scheduledUtc);

            if (conflict)
            {
                TempData["StatusMessage"] = "This slot overlaps with another appointment.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            var appointment = new Appointment
            {
                PatientId = attachment.PatientId,
                DoctorId = doctor.Id,
                ScheduledAt = DateTime.SpecifyKind(scheduledUtc, DateTimeKind.Utc),
                StartTimeUtc = DateTime.SpecifyKind(scheduledUtc, DateTimeKind.Utc),
                Reason = string.IsNullOrWhiteSpace(attachment.PatientNotes) ? "-" : attachment.PatientNotes,
                Location = string.IsNullOrWhiteSpace(Location) ? "Clinic" : Location.Trim(),
                Status = AppointmentStatus.Approved,
                VisitStage = VisitStage.NotArrived,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);

            attachment.Status = AttachmentStatus.Validated;
            attachment.ValidatedAtUtc = DateTime.UtcNow;
            attachment.ValidationNotes =
                $"APPROVED_BY_DOCTOR|Selected:{requestedIso}|Scheduled:{requestedIso}" +
                (string.IsNullOrWhiteSpace(DecisionNote) ? "" : $"|Note:{DecisionNote.Trim()}");

            await _db.SaveChangesAsync();

            var patientUser = attachment.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Appointment Approved",
                    $"Your appointment request was approved for <b>{localKinded:ddd dd MMM HH:mm}</b>.",
                    actionUrl: "/Patient/Appointments/Index",
                    actionText: "View Appointments",
                    relatedEntity: "Appointment",
                    relatedEntityId: appointment.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Appointment approved successfully.";
            return RedirectToPage("/Doctor/Appointments/Approvals");
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
                return Forbid();

            var attachment = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == AttachmentId &&
                    a.Type == "AppointmentRequest" &&
                    a.DoctorId == doctor.Id);

            if (attachment == null)
                return NotFound();

            if (attachment.Status != AttachmentStatus.Pending)
            {
                TempData["StatusMessage"] = "This request has already been processed.";
                return RedirectToPage("/Doctor/Appointments/Approvals");
            }

            attachment.Status = AttachmentStatus.Rejected;
            attachment.ValidatedAtUtc = DateTime.UtcNow;
            attachment.ValidationNotes =
                "REJECTED_BY_DOCTOR" +
                (string.IsNullOrWhiteSpace(DecisionNote) ? "" : $"|Note:{DecisionNote.Trim()}");

            await _db.SaveChangesAsync();

            var patientUser = attachment.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Appointment Rejected",
                    "Your appointment request was rejected by the doctor.",
                    actionUrl: "/Patient/Appointments/Index",
                    actionText: "View Appointments",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Appointment request rejected.";
            return RedirectToPage("/Doctor/Appointments/Approvals");
        }

        private static string ExtractRequestedIso(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return "";

            var idx = notes.IndexOf("Selected:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return "";

            var value = notes[(idx + "Selected:".Length)..].Trim();
            if (value.Contains('|'))
                value = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];

            return string.IsNullOrWhiteSpace(value) ? "" : value;
        }

        private static string FormatLocalIsoForDisplay(string? localIso)
        {
            if (string.IsNullOrWhiteSpace(localIso))
                return "-";

            if (!DateTime.TryParseExact(
                    localIso,
                    new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
                return localIso;

            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToString("ddd dd MMM HH:mm");
        }
    }
}
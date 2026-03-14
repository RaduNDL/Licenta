using System;
using System.ComponentModel.DataAnnotations;
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
    public class RescheduleReviewModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public RescheduleReviewModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        [BindProperty(SupportsGet = true)]
        public int RequestId { get; set; }

        [BindProperty]
        [StringLength(200)]
        public string? DecisionNote { get; set; }

        public bool CanView { get; set; }

        public string PatientName { get; set; } = "Patient";
        public string OldTime { get; set; } = "";
        public string SelectedTime { get; set; } = "";
        public string Location { get; set; } = "Clinic";
        public string Reason { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            CanView = false;
            PatientName = "Patient";
            OldTime = "";
            SelectedTime = "";
            Location = "Clinic";
            Reason = "";

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            // Fully-qualified type to avoid ambiguity/namespace conflicts
            var doctor = await _db.Set<Licenta.Models.DoctorProfile>().AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return;

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Include(r => r.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
                .Include(r => r.SelectedOption)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.DoctorId == doctor.Id);

            if (req == null) return;

            CanView = true;
            PatientName = req.Patient?.User?.FullName ?? req.Patient?.User?.Email ?? "Patient";
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? "-" : req.Reason;

            // Old time - OldScheduledAtUtc is stored as UTC -> convert to local for display
            OldTime = req.OldScheduledAtUtc == default ? "-" : req.OldScheduledAtUtc.ToLocalTime().ToString("ddd dd MMM HH:mm");

            if (req.SelectedOption != null)
            {
                // ProposedStartUtc is stored in UTC; convert to local for display
                SelectedTime = req.SelectedOption.ProposedStartUtc.ToLocalTime().ToString("ddd dd MMM HH:mm");
                Location = string.IsNullOrWhiteSpace(req.SelectedOption.Location) ? Location : req.SelectedOption.Location;
            }
            else if (req.NewScheduledAtUtc.HasValue)
            {
                SelectedTime = req.NewScheduledAtUtc.Value.ToLocalTime().ToString("ddd dd MMM HH:mm");
            }
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Set<Licenta.Models.DoctorProfile>()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return Forbid();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Appointment)
                .Include(r => r.SelectedOption)
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.DoctorId == doctor.Id);

            if (req == null) return NotFound();

            if (req.Status != AppointmentRescheduleStatus.PatientSelected || req.SelectedOptionId == null || req.SelectedOption == null)
            {
                TempData["StatusMessage"] = "This request cannot be approved.";
                return RedirectToPage(new { requestId = RequestId });
            }

            var opt = req.SelectedOption;

            // opt.ProposedStartUtc / ProposedEndUtc are expected to be UTC in DB.
            // Make sure the DateTimeKind is Utc to avoid later conversion issues.
            var proposedStartUtc = DateTime.SpecifyKind(opt.ProposedStartUtc, DateTimeKind.Utc);
            var proposedEndUtc = DateTime.SpecifyKind(opt.ProposedEndUtc, DateTimeKind.Utc);

            var conflict = await _db.Set<Appointment>()
                .AnyAsync(a =>
                    a.DoctorId == req.DoctorId &&
                    a.Id != req.AppointmentId &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.Completed &&
                    // Ensure a.StartTimeUtc is treated as UTC
                    DateTime.SpecifyKind(a.StartTimeUtc, DateTimeKind.Utc) < proposedEndUtc &&
                    proposedStartUtc < DateTime.SpecifyKind(a.StartTimeUtc, DateTimeKind.Utc).AddMinutes(30));

            if (conflict)
            {
                TempData["StatusMessage"] = "Conflict detected. The selected option overlaps with another appointment.";
                return RedirectToPage(new { requestId = RequestId });
            }

            var appt = req.Appointment;
            if (appt == null)
            {
                TempData["StatusMessage"] = "Associated appointment not found.";
                return RedirectToPage(new { requestId = RequestId });
            }

            // Save UTC explicitly
            appt.ScheduledAt = DateTime.SpecifyKind(proposedStartUtc, DateTimeKind.Utc);
            appt.StartTimeUtc = DateTime.SpecifyKind(proposedStartUtc, DateTimeKind.Utc);
            appt.Location = string.IsNullOrWhiteSpace(opt.Location) ? appt.Location : opt.Location;
            appt.RescheduleReason = req.Reason;
            appt.Status = AppointmentStatus.Rescheduled;
            appt.VisitStage = VisitStage.NotArrived;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            req.NewScheduledAtUtc = DateTime.SpecifyKind(proposedStartUtc, DateTimeKind.Utc);
            req.DoctorDecisionNote = string.IsNullOrWhiteSpace(DecisionNote) ? null : DecisionNote.Trim();
            req.Status = AppointmentRescheduleStatus.Approved;
            req.ApprovedAtUtc = DateTime.UtcNow;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var patientUser = req.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Reschedule Approved",
                    $"Your request to reschedule with Dr. {doctor.User?.FullName} was approved. New time: <b>{proposedStartUtc.ToLocalTime():ddd dd MMM HH:mm}</b>.",
                    actionUrl: "/Patient/Appointments/Index",
                    actionText: "View Appointments",
                    relatedEntity: "Appointment",
                    relatedEntityId: appt.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Approved. Appointment rescheduled.";
            return RedirectToPage("/Doctor/Appointments/RescheduleApprovals");
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Set<Licenta.Models.DoctorProfile>()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return Forbid();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.DoctorId == doctor.Id);

            if (req == null)
            {
                TempData["StatusMessage"] = "Request not found.";
                return RedirectToPage("/Doctor/Appointments/RescheduleApprovals");
            }

            if (req.Status == AppointmentRescheduleStatus.Approved || req.Status == AppointmentRescheduleStatus.Rejected || req.Status == AppointmentRescheduleStatus.Cancelled)
            {
                TempData["StatusMessage"] = "This request cannot be rejected.";
                return RedirectToPage(new { requestId = RequestId });
            }

            req.Status = AppointmentRescheduleStatus.Rejected;
            req.DoctorDecisionNote = string.IsNullOrWhiteSpace(DecisionNote) ? null : DecisionNote.Trim();
            req.RejectedAtUtc = DateTime.UtcNow;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var patientUser = req.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Appointment,
                    "Reschedule Rejected",
                    $"Your reschedule request with Dr. {doctor.User?.FullName} was rejected.",
                    actionUrl: "/Patient/Appointments/Index",
                    actionText: "View Appointments",
                    relatedEntity: "AppointmentRescheduleRequest",
                    relatedEntityId: req.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Rejected.";
            return RedirectToPage("/Doctor/Appointments/RescheduleApprovals");
        }
    }
}
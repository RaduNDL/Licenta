using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
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

        public RescheduleReviewModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
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

        public async Task<IActionResult> OnPostApproveAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Set<Licenta.Models.DoctorProfile>()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return Forbid();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Appointment)
                .Include(r => r.SelectedOption)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.DoctorId == doctor.Id);

            if (req == null) return NotFound();

            if (req.Status != AppointmentRescheduleStatus.PatientSelected || req.SelectedOptionId == null || req.SelectedOption == null)
            {
                TempData["StatusMessage"] = "This request cannot be approved.";
                return RedirectToPage(new { requestId = RequestId });
            }

            var opt = req.SelectedOption;

            var conflict = await _db.Set<Appointment>()
                .AnyAsync(a =>
                    a.DoctorId == req.DoctorId &&
                    a.Id != req.AppointmentId &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.Completed &&
                    a.StartTimeUtc < opt.ProposedEndUtc &&
                    opt.ProposedStartUtc < a.StartTimeUtc.AddMinutes(30));

            if (conflict)
            {
                TempData["StatusMessage"] = "Conflict detected. The selected option overlaps with another appointment.";
                return RedirectToPage(new { requestId = RequestId });
            }

            var appt = req.Appointment;

            appt.ScheduledAt = opt.ProposedStartUtc;
            appt.StartTimeUtc = opt.ProposedStartUtc;
            appt.Location = string.IsNullOrWhiteSpace(opt.Location) ? appt.Location : opt.Location;
            appt.RescheduleReason = req.Reason;
            appt.Status = AppointmentStatus.Rescheduled;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            req.NewScheduledAtUtc = opt.ProposedStartUtc;
            req.DoctorDecisionNote = string.IsNullOrWhiteSpace(DecisionNote) ? null : DecisionNote.Trim();
            req.Status = AppointmentRescheduleStatus.Approved;
            req.ApprovedAtUtc = DateTime.UtcNow;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Approved. Appointment rescheduled.";
            return RedirectToPage("/Doctor/Appointments/RescheduleApprovals");
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Set<Licenta.Models.DoctorProfile>()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return Forbid();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.DoctorId == doctor.Id);

            if (req == null) return NotFound();

            if (req.Status != AppointmentRescheduleStatus.PatientSelected)
            {
                TempData["StatusMessage"] = "This request cannot be rejected.";
                return RedirectToPage(new { requestId = RequestId });
            }

            req.DoctorDecisionNote = string.IsNullOrWhiteSpace(DecisionNote) ? "Rejected by doctor." : DecisionNote.Trim();
            req.Status = AppointmentRescheduleStatus.Rejected;
            req.RejectedAtUtc = DateTime.UtcNow;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Rejected.";
            return RedirectToPage("/Doctor/Appointments/RescheduleApprovals");
        }

        private async Task LoadAsync()
        {
            CanView = false;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var doctor = await _db.Set<Licenta.Models.DoctorProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return;

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.SelectedOption)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.DoctorId == doctor.Id);

            if (req == null) return;

            CanView = true;

            PatientName = req.Patient?.User?.FullName ?? req.Patient?.User?.Email ?? "Patient";
            OldTime = req.OldScheduledAtUtc.ToString("yyyy-MM-dd HH:mm");
            Reason = req.Reason;
            DecisionNote = req.DoctorDecisionNote;

            if (req.SelectedOption != null)
            {
                SelectedTime = $"{req.SelectedOption.ProposedStartUtc:yyyy-MM-dd HH:mm} - {req.SelectedOption.ProposedEndUtc:HH:mm}";
                Location = req.SelectedOption.Location;
            }
        }
    }
}

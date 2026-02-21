using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
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
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public RescheduleModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty]
        [Required]
        [StringLength(500)]
        public string InputReason { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [StringLength(1000)]
        public string InputPreferredWindows { get; set; } = string.Empty;

        public bool CanRequest { get; set; }

        public string DoctorName { get; set; } = "Doctor";
        public string CurrentScheduledAtDisplay { get; set; } = "";
        public string Location { get; set; } = "Clinic";

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
                .Include(a => a.Doctor)
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
                DoctorName = "Doctor";
                CurrentScheduledAtDisplay = appt.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                Location = string.IsNullOrWhiteSpace(appt.Location) ? "Clinic" : appt.Location;
                CanRequest = true;
                return Page();
            }

            var req = new AppointmentRescheduleRequest
            {
                AppointmentId = appt.Id,
                PatientId = patient.Id,
                DoctorId = appt.DoctorId,
                Status = AppointmentRescheduleStatus.Requested,
                Reason = InputReason.Trim(),
                PreferredWindows = InputPreferredWindows.Trim(),
                OldScheduledAtUtc = appt.ScheduledAt,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Add(req);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Reschedule request submitted.";
            return RedirectToPage("/Patient/Appointments/ReschedulePick", new { requestId = req.Id });
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

using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class AssistantAppointmentsCancelPageModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notifier;
        private readonly UserManager<ApplicationUser> _userManager;

        public AssistantAppointmentsCancelPageModel(
            AppDbContext db,
            INotificationService notifier,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _notifier = notifier;
            _userManager = userManager;
        }

        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;

        public class InputModel
        {
            [Required]
            [Display(Name = "Cancel reason")]
            public string CancelReason { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return Unauthorized();

            var appointment = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                var patientClinic = appointment.Patient?.User?.ClinicId;
                var doctorClinic = appointment.Doctor?.User?.ClinicId;
                if (patientClinic != assistant.ClinicId || doctorClinic != assistant.ClinicId)
                    return Forbid();
            }

            PatientName = appointment.Patient?.User?.FullName
                ?? appointment.Patient?.User?.Email
                ?? "Unknown patient";

            DoctorName = appointment.Doctor?.User?.FullName
                ?? appointment.Doctor?.User?.Email
                ?? "Unknown doctor";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return Unauthorized();

            var appointment = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                var patientClinic = appointment.Patient?.User?.ClinicId;
                var doctorClinic = appointment.Doctor?.User?.ClinicId;
                if (patientClinic != assistant.ClinicId || doctorClinic != assistant.ClinicId)
                    return Forbid();
            }

            PatientName = appointment.Patient?.User?.FullName
                ?? appointment.Patient?.User?.Email
                ?? "Unknown patient";

            DoctorName = appointment.Doctor?.User?.FullName
                ?? appointment.Doctor?.User?.Email
                ?? "Unknown doctor";

            if (!ModelState.IsValid)
                return Page();

            if (appointment.ScheduledAt <= DateTime.UtcNow)
            {
                TempData["StatusMessage"] = "You cannot cancel an appointment in the past.";
                return RedirectToPage("/Assistant/Appointments/Index",
                    new { view = "day", date = DateTime.Today.ToString("yyyy-MM-dd") });
            }

            if (appointment.Status == AppointmentStatus.Cancelled)
            {
                TempData["StatusMessage"] = "This appointment is already cancelled.";
                return RedirectToPage("/Assistant/Appointments/Index",
                    new { view = "day", date = appointment.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd") });
            }

            if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.NoShow)
            {
                TempData["StatusMessage"] = "You cannot cancel a finished appointment.";
                return RedirectToPage("/Assistant/Appointments/Index",
                    new { view = "day", date = appointment.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd") });
            }

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelReason = Input.CancelReason;
            appointment.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var safeReason = System.Net.WebUtility.HtmlEncode(appointment.CancelReason);

            if (appointment.Patient?.User != null)
            {
                await _notifier.NotifyAsync(
                    appointment.Patient.User,
                    NotificationType.Appointment,
                    "Appointment cancelled",
                    $"Your appointment on <b>{appointment.ScheduledAt.ToLocalTime():f}</b> was cancelled.<br/>Reason: {safeReason}",
                    relatedEntity: "Appointment",
                    relatedEntityId: appointment.Id.ToString(),
                    sendEmail: false
                );
            }

            if (appointment.Doctor?.User != null)
            {
                var patientLabel = appointment.Patient?.User?.FullName ?? appointment.Patient?.User?.Email ?? "Patient";
                await _notifier.NotifyAsync(
                    appointment.Doctor.User,
                    NotificationType.Appointment,
                    "Appointment cancelled",
                    $"Patient: <b>{System.Net.WebUtility.HtmlEncode(patientLabel)}</b><br/>When: <b>{appointment.ScheduledAt.ToLocalTime():f}</b><br/>Reason: {safeReason}",
                    relatedEntity: "Appointment",
                    relatedEntityId: appointment.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] = "Appointment cancelled successfully.";

            return RedirectToPage("/Assistant/Appointments/Index",
                new { view = "day", date = appointment.ScheduledAt.ToLocalTime().ToString("yyyy-MM-dd") });
        }
    }
}

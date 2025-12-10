using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
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

        public AssistantAppointmentsCancelPageModel(AppDbContext db, INotificationService notifier)
        {
            _db = db;
            _notifier = notifier;
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
            var appointment = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
                return NotFound();

            PatientName = appointment.Patient.User.FullName ?? appointment.Patient.User.Email ?? "Unknown patient";
            DoctorName = appointment.Doctor.User.FullName ?? appointment.Doctor.User.Email ?? "Unknown doctor";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
                return NotFound();

            if (!ModelState.IsValid)
                return Page();

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelReason = Input.CancelReason;

            await _db.SaveChangesAsync();

            if (appointment.Patient?.User != null)
            {
                await _notifier.NotifyAsync(
                    appointment.Patient.User,
                    NotificationType.Appointment,
                    "Appointment cancelled",
                    $"Your appointment on {appointment.ScheduledAt.ToLocalTime():f} was cancelled.<br/>Reason: {appointment.CancelReason}",
                    relatedEntity: "Appointment",
                    relatedEntityId: appointment.Id.ToString()
                );
            }

            if (appointment.Doctor?.User != null)
            {
                await _notifier.NotifyAsync(
                    appointment.Doctor.User,
                    NotificationType.Appointment,
                    "Appointment cancelled",
                    $"Patient: {appointment.Patient.User.FullName ?? appointment.Patient.User.Email}.<br/>Reason: {appointment.CancelReason}",
                    relatedEntity: "Appointment",
                    relatedEntityId: appointment.Id.ToString()
                );
            }

            TempData["StatusMessage"] = "Appointment cancelled successfully.";
            return RedirectToPage("/Doctor/Appointments/Index", new { view = "day", date = appointment.ScheduledAt.ToString("yyyy-MM-dd") });
        }
    }
}

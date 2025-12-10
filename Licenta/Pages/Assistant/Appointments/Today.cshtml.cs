using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class TodayModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TodayModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<AppointmentRow> Appointments { get; set; } = new();

        public class AppointmentRow
        {
            public int Id { get; set; }
            public string TimeLocal { get; set; } = string.Empty;
            public string PatientName { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public VisitStage VisitStage { get; set; }
        }

        public async Task OnGetAsync()
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            Appointments = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.ScheduledAt >= today && a.ScheduledAt < tomorrow)
                .OrderBy(a => a.ScheduledAt)
                .Select(a => new AppointmentRow
                {
                    Id = a.Id,
                    TimeLocal = a.ScheduledAt.ToLocalTime().ToString("t"),
                    PatientName = a.Patient.User.FullName ?? a.Patient.User.Email!,
                    DoctorName = a.Doctor.User.FullName ?? a.Doctor.User.Email!,
                    Reason = a.Reason ?? string.Empty,
                    Status = a.Status.ToString(),
                    VisitStage = a.VisitStage
                })
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostUpdateStageAsync(int appointmentId, string stage)
        {
            if (!Enum.TryParse<VisitStage>(stage, out var newStage))
            {
                TempData["StatusMessage"] = "Invalid visit stage.";
                return RedirectToPage();
            }

            var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appt == null)
            {
                TempData["StatusMessage"] = "Appointment not found.";
                return RedirectToPage();
            }

            appt.VisitStage = newStage;
            appt.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"Visit stage updated to '{newStage}'.";
            return RedirectToPage();
        }
    }
}

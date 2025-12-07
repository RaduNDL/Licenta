using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Appointments
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<Appointment> Appointments { get; set; } = new();
        public string ViewMode { get; set; } = "day";   // "day" sau "week"
        public DateTime SelectedDate { get; set; }

        // patientId este Guid?, compatibil cu Appointment.PatientId (Guid)
        public async Task OnGetAsync(string? view, DateTime? date, Guid? patientId)
        {
            // modul de vizualizare
            ViewMode = string.IsNullOrEmpty(view) ? "day" : view.ToLowerInvariant();
            SelectedDate = (date ?? DateTime.UtcNow).Date;

            // utilizatorul curent
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                Appointments = new();
                return;
            }

            // găsim DoctorProfile pentru user-ul curent
            var doctorProfile = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

            if (doctorProfile == null)
            {
                Appointments = new();
                return;
            }

            var doctorId = doctorProfile.Id; // Guid

            // query de bază: programările doctorului curent
            var query = _db.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctorId);   // Guid == Guid

            // filtru opțional după pacient
            if (patientId.HasValue)
            {
                query = query.Where(a => a.PatientId == patientId.Value); // Guid == Guid
            }

            // interval de timp în funcție de day/week
            DateTime start, end;

            if (ViewMode == "week")
            {
                var diff = (int)SelectedDate.DayOfWeek;
                start = SelectedDate.AddDays(-diff).Date;
                end = start.AddDays(7);
            }
            else // day
            {
                start = SelectedDate.Date;
                end = start.AddDays(1);
            }

            query = query.Where(a => a.ScheduledAt >= start && a.ScheduledAt < end);

            Appointments = await query
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync();
        }
    }
}

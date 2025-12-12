using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public string ViewMode { get; set; } = "day";
        public DateTime SelectedDate { get; set; }

        public class HourSlot
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public List<Appointment> Appointments { get; set; } = new();
        }

        public List<HourSlot> HourSchedule { get; set; } = new();

        public async Task OnGetAsync(string? view, DateTime? date, Guid? patientId)
        {
            ViewMode = string.IsNullOrEmpty(view) ? "day" : view.ToLowerInvariant();
            SelectedDate = (date ?? DateTime.UtcNow).Date;

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                Appointments = new();
                HourSchedule = new();
                return;
            }

            var doctorProfile = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

            if (doctorProfile == null)
            {
                Appointments = new();
                HourSchedule = new();
                return;
            }

            var doctorId = doctorProfile.Id;

            var query = _db.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctorId);

            if (patientId.HasValue)
            {
                query = query.Where(a => a.PatientId == patientId.Value);
            }

            DateTime start;
            DateTime end;

            if (ViewMode == "week")
            {
                var diff = (int)SelectedDate.DayOfWeek;
                start = SelectedDate.AddDays(-diff).Date;
                end = start.AddDays(7);
            }
            else
            {
                start = SelectedDate.Date;
                end = start.AddDays(1);
            }

            query = query.Where(a => a.ScheduledAt >= start && a.ScheduledAt < end);

            Appointments = await query
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync();

            HourSchedule = new List<HourSlot>();

            if (ViewMode == "day")
            {
                var workStart = SelectedDate.Date.AddHours(8);
                var workEnd = SelectedDate.Date.AddHours(17);

                for (var t = workStart; t < workEnd; t = t.AddHours(1))
                {
                    var slotStart = t;
                    var slotEnd = t.AddHours(1);

                    var slotAppointments = Appointments
                        .Where(a => a.ScheduledAt >= slotStart && a.ScheduledAt < slotEnd)
                        .ToList();

                    HourSchedule.Add(new HourSlot
                    {
                        Start = slotStart,
                        End = slotEnd,
                        Appointments = slotAppointments
                    });
                }
            }
        }
    }
}

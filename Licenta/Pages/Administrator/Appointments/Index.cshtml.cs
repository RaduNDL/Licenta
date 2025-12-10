using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Admin.Appointments
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<Appointment> Appointments { get; set; } = new();
        public DateTime? SelectedDate { get; set; }
        public Guid? DoctorId { get; set; }

        public async Task OnGetAsync(DateTime? date, Guid? doctorId)
        {
            SelectedDate = date;
            DoctorId = doctorId;

            var query = _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (SelectedDate.HasValue)
            {
                var day = SelectedDate.Value.Date;
                var nextDay = day.AddDays(1);
                query = query.Where(a => a.ScheduledAt >= day && a.ScheduledAt < nextDay);
            }

            if (DoctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == DoctorId.Value);
            }

            Appointments = await query
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync();
        }
    }
}

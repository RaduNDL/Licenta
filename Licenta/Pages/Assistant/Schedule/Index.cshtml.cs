using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Schedule
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<DoctorVm> Doctors { get; set; } = new();

        public class DoctorVm
        {
            public string DoctorName { get; set; } = "";
            public List<DoctorAvailability> Schedule { get; set; } = new();
        }

        public async Task OnGetAsync()
        {
            var list = await _db.Doctors
                .Include(d => d.User)
                .Include(d => d.Availabilities)
                .AsNoTracking()
                .ToListAsync();

            Doctors = list.Select(d => new DoctorVm
            {
                DoctorName = d.User.FullName ?? d.User.Email ?? "Doctor",
                Schedule = d.Availabilities
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.DayOfWeek)
                    .ToList()
            }).ToList();
        }
    }
}

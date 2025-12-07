using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Patients
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<PatientProfile> Patients { get; set; } = new();

        public async Task OnGetAsync()
        {
            Patients = await _db.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName ?? p.User.Email)
                .ToListAsync();
        }
    }
}

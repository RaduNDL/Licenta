using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;


namespace Licenta.Pages.Doctor.Patients
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

        public List<PatientProfile> Patients { get; set; } = new();

        public async Task OnGetAsync()
        {
            var doctorUser = await _userManager.GetUserAsync(User);
            if (doctorUser == null)
            {
                Patients = new();
                return;
            }

            var clinicId = (doctorUser.ClinicId ?? "").Trim();

            var query = _db.Patients
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.User != null && !p.User.IsSoftDeleted);

            if (!string.IsNullOrWhiteSpace(clinicId))
                query = query.Where(p => (p.User!.ClinicId ?? "").Trim() == clinicId);

            Patients = await query
                .OrderBy(p => (p.User!.FullName ?? p.User.Email ?? p.User.UserName ?? ""))
                .ToListAsync();
        }
    }
}

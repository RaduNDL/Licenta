using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.MedicalRecords
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IList<MedicalRecord> Records { get; set; } = new List<MedicalRecord>();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
                return;

            Records = await _db.MedicalRecords
                .AsNoTracking()
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Where(r =>
                    r.PatientId == patient.Id &&
                    r.Status == RecordStatus.Validated)
                .OrderByDescending(r => r.VisitDateUtc)
                .Take(300)
                .ToListAsync();
        }
    }
}

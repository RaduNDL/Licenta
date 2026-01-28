using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Patients
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IList<PatientProfile> Patients { get; set; } = new List<PatientProfile>();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            Patients = await _db.Patients
                .Include(p => p.User)
                .Where(p => p.User.ClinicId == user.ClinicId)
                .OrderBy(p => p.User.FullName)
                .ToListAsync();
        }
    }
}

using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
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

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public IList<PatientProfile> Patients { get; set; } = new List<PatientProfile>();

        public async Task OnGetAsync()
        {
            Patients = await _db.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName)
                .ToListAsync();
        }
    }
}

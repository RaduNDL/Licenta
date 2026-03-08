using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Predictions
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

        public List<Prediction> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null) return;

            Items = await _db.Predictions
                .AsNoTracking()
                .Where(p => p.PatientId == patient.Id && p.Status == PredictionStatus.Accepted)
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync();
        }
    }
}
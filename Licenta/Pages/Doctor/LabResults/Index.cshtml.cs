using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.LabResults
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

        public IList<LabResult> Results { get; set; } = new List<LabResult>();

        public async Task<IActionResult> OnGetAsync(Guid? patientId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                return Page();
            }

            var query = _db.LabResults
                .Include(l => l.Patient).ThenInclude(p => p.User)
                .Include(l => l.ValidatedByDoctor)
                .Where(l =>
                    string.IsNullOrWhiteSpace(user.ClinicId) ||
                    l.Patient.User.ClinicId == user.ClinicId);

            if (patientId.HasValue)
            {
                query = query.Where(l => l.PatientId == patientId.Value);
            }

            Results = await query
                .OrderByDescending(l => l.UploadedAt)
                .ToListAsync();

            return Page();
        }
    }
}

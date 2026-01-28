using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.MedicalRecords
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

        public IList<MedicalRecord> Records { get; set; } = new List<MedicalRecord>();
        public Guid? SelectedPatientId { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? patientId)
        {
            SelectedPatientId = patientId;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                Records = new List<MedicalRecord>();
                TempData["StatusMessage"] = "Doctor profile not found. Please create a DoctorProfile for this user.";
                return Page();
            }

            var query = _db.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Where(r => r.DoctorId == doctor.Id);

            if (patientId.HasValue)
                query = query.Where(r => r.PatientId == patientId.Value);

            Records = await query
                .OrderByDescending(r => r.VisitDateUtc)
                .ToListAsync();

            return Page();
        }
    }
}

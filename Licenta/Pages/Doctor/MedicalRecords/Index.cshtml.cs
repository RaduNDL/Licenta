using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Licenta.Pages.Doctor.MedicalRecords
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<MedicalRecord> Records { get; set; } = new List<MedicalRecord>();

        public async Task<IActionResult> OnGetAsync(Guid? patientId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                Records = new List<MedicalRecord>();
                TempData["StatusMessage"] = "Doctor profile not found. Please create a DoctorProfile for this user.";
                return Page();
            }

            var query = _context.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Where(r => r.DoctorId == doctor.Id);

            if (patientId.HasValue)
            {
                query = query.Where(r => r.PatientId == patientId.Value);
            }

            Records = await query
                .OrderByDescending(r => r.VisitDateUtc)
                .ToListAsync();

            return Page();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.LabResults
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

        public IList<LabResult> Results { get; set; } = new List<LabResult>();

        public async Task<IActionResult> OnGetAsync(Guid? patientId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _context.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found. Please create a DoctorProfile for this user.";
                Results = new List<LabResult>();
                return Page();
            }

            // deocamdată doctorul vede toate analizele; 
            // dacă legi pacienții de doctor, poți filtra aici
            var query = _context.LabResults
                .Include(l => l.Patient).ThenInclude(p => p.User)
                .AsQueryable();

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

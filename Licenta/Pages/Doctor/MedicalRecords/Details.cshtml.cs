using System;
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
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public MedicalRecord Record { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var doctor = await _context.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            var record = await _context.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
                return NotFound();

            if (doctor == null || record.DoctorId != doctor.Id)
                return Forbid();

            Record = record;
            return Page();
        }

        public async Task<IActionResult> OnPostValidateAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            var record = await _context.MedicalRecords
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
                return NotFound();

            if (doctor == null || record.DoctorId != doctor.Id)
                return Forbid();

            record.Status = RecordStatus.Validated;
            record.ValidatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }
    }
}
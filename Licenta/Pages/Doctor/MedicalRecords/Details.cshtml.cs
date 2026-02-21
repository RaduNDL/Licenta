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
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public MedicalRecord Record { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            var record = await _db.MedicalRecords
                .AsNoTracking()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();
            if (record.DoctorId != doctor.Id) return Forbid();

            Record = record;
            return Page();
        }

        public async Task<IActionResult> OnPostValidateAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            var record = await _db.MedicalRecords.FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();
            if (record.DoctorId != doctor.Id) return Forbid();

            if (record.Status != RecordStatus.Validated)
            {
                record.Status = RecordStatus.Validated;
                record.ValidatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = "Record validated. The patient can now see it in their portal.";
            }

            return RedirectToPage(new { id });
        }
    }
}

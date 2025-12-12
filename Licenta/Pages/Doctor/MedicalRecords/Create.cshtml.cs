using System;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.MedicalRecords
{
    [Authorize(Roles = "Doctor")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public MedicalRecord Record { get; set; } = new();

        public SelectList Patients { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? patientId)
        {
            var patientList = await _context.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName)
                .ToListAsync();

            Patients = new SelectList(
                patientList,
                nameof(PatientProfile.Id),
                "User.FullName",
                patientId
            );

            if (patientId.HasValue)
                Record.PatientId = patientId.Value;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return Page();
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                return Page();
            }

            Record.Id = Guid.NewGuid();
            Record.DoctorId = doctor.Id;
            Record.VisitDateUtc = DateTime.UtcNow;
            Record.Status = RecordStatus.Draft;

            _context.MedicalRecords.Add(Record);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Doctor/MedicalRecords/Details", new { id = Record.Id });
        }
    }
}
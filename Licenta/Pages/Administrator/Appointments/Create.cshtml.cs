using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Admin.Appointments
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;

        public CreateModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public Appointment Appointment { get; set; } = new();

        public List<DoctorProfile> Doctors { get; set; } = new();
        public List<PatientProfile> Patients { get; set; } = new();

        public async Task OnGetAsync()
        {
            Doctors = await _db.Doctors
                .Include(d => d.User)
                .OrderBy(d => d.User.FullName)
                .ToListAsync();

            Patients = await _db.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Doctors = await _db.Doctors
                    .Include(d => d.User)
                    .OrderBy(d => d.User.FullName)
                    .ToListAsync();

                Patients = await _db.Patients
                    .Include(p => p.User)
                    .OrderBy(p => p.User.FullName)
                    .ToListAsync();

                return Page();
            }

            _db.Appointments.Add(Appointment);
            await _db.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}

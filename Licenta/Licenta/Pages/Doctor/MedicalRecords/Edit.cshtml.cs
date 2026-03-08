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
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty]
        public MedicalRecord Record { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            var record = await _db.MedicalRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();
            if (record.DoctorId != doctor.Id) return Forbid();
            if (record.Status != RecordStatus.Draft) return Forbid();

            Record = record;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Record.PatientId");
            ModelState.Remove("Record.Patient");
            ModelState.Remove("Record.DoctorId");
            ModelState.Remove("Record.Doctor");
            ModelState.Remove("Record.Appointment");
            ModelState.Remove("Record.AppointmentId");
            ModelState.Remove("Record.VisitDateUtc");
            ModelState.Remove("Record.Status");
            ModelState.Remove("Record.ValidatedAtUtc");

            if (!ModelState.IsValid) return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            var dbRecord = await _db.MedicalRecords.FirstOrDefaultAsync(r => r.Id == Record.Id);
            if (dbRecord == null) return NotFound();
            if (dbRecord.DoctorId != doctor.Id) return Forbid();
            if (dbRecord.Status != RecordStatus.Draft) return Forbid();

            dbRecord.Diagnosis = Record.Diagnosis;
            dbRecord.Symptoms = Record.Symptoms;
            dbRecord.Notes = Record.Notes;
            dbRecord.Treatment = Record.Treatment;

            await _db.SaveChangesAsync();
            return RedirectToPage("/Doctor/MedicalRecords/Details", new { id = dbRecord.Id });
        }
    }
}

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
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty]
        public MedicalRecord Record { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? AppointmentId { get; set; }

        public Appointment? Appointment { get; set; }

        public SelectList Patients { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? appointmentId, Guid? patientId)
        {
            AppointmentId = appointmentId;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            if (AppointmentId.HasValue)
            {
                var appt = await _db.Appointments
                    .Include(a => a.Patient).ThenInclude(p => p.User)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == AppointmentId.Value);

                if (appt == null) return NotFound();
                if (appt.DoctorId != doctor.Id) return Forbid();

                var existingId = await _db.MedicalRecords
                    .AsNoTracking()
                    .Where(r => r.AppointmentId == AppointmentId.Value)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync();

                if (existingId.HasValue)
                    return RedirectToPage("/Doctor/MedicalRecords/Details", new { id = existingId.Value });

                Appointment = appt;
                Record.PatientId = appt.PatientId;

                return Page();
            }

            await LoadPatientsAsync(patientId);
            if (patientId.HasValue)
                Record.PatientId = patientId.Value;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            if (AppointmentId.HasValue)
            {
                var appt = await _db.Appointments
                    .Include(a => a.Patient).ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(a => a.Id == AppointmentId.Value);

                if (appt == null) return NotFound();
                if (appt.DoctorId != doctor.Id) return Forbid();

                var existingId = await _db.MedicalRecords
                    .Where(r => r.AppointmentId == AppointmentId.Value)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync();

                if (existingId.HasValue)
                    return RedirectToPage("/Doctor/MedicalRecords/Details", new { id = existingId.Value });

                Appointment = appt;
                Record.PatientId = appt.PatientId;
                Record.AppointmentId = AppointmentId.Value;
                Record.VisitDateUtc = appt.ScheduledAt;
            }
            else
            {
                if (Record.PatientId == Guid.Empty)
                    ModelState.AddModelError("Record.PatientId", "Please select a patient.");

                Record.VisitDateUtc = DateTime.UtcNow;
            }

            Record.DoctorId = doctor.Id;
            Record.Status = RecordStatus.Draft;
            Record.ValidatedAtUtc = null;

            ModelState.Remove("Record.DoctorId");
            ModelState.Remove("Record.Doctor");
            ModelState.Remove("Record.Patient");
            ModelState.Remove("Record.Appointment");
            ModelState.Remove("Record.Status");
            ModelState.Remove("Record.ValidatedAtUtc");
            ModelState.Remove("Record.VisitDateUtc");
            ModelState.Remove("Record.AppointmentId");
            ModelState.Remove("Record.Id");

            if (!ModelState.IsValid)
            {
                if (AppointmentId.HasValue)
                {
                    if (Appointment == null)
                    {
                        Appointment = await _db.Appointments
                            .Include(a => a.Patient).ThenInclude(p => p.User)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.Id == AppointmentId.Value);
                    }
                }
                else
                {
                    await LoadPatientsAsync(Record.PatientId);
                }

                return Page();
            }

            Record.Id = Guid.NewGuid();

            _db.MedicalRecords.Add(Record);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Draft saved. Validate to make it visible to the patient.";
            return RedirectToPage("/Doctor/MedicalRecords/Details", new { id = Record.Id });
        }

        private async Task LoadPatientsAsync(Guid? selectedPatientId)
        {
            var patients = await _db.Patients
                .Include(p => p.User)
                .AsNoTracking()
                .OrderBy(p => p.User.FullName ?? p.User.Email)
                .Select(p => new
                {
                    p.Id,
                    Name = (p.User.FullName ?? p.User.Email) ?? "Patient"
                })
                .ToListAsync();

            Patients = new SelectList(patients, "Id", "Name", selectedPatientId);
        }
    }
}

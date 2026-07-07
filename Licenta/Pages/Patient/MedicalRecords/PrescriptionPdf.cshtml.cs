using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.MedicalRecords
{
    [Authorize(Roles = "Patient")]
    public class PrescriptionPdfModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPdfService _pdf;

        public PrescriptionPdfModel(AppDbContext db, UserManager<ApplicationUser> userManager, IPdfService pdf)
        {
            _db = db;
            _userManager = userManager;
            _pdf = pdf;
        }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (id == null || id == Guid.Empty) return NotFound();

            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null) return Forbid();

            var record = await _db.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == id.Value);

            if (record == null) return NotFound();
            if (record.PatientId != patient.Id) return Forbid();
            if (record.Status != RecordStatus.Validated) return Forbid();

            var pdfBytes = _pdf.GeneratePrescription(record);


            var patientName = SanitizeFileName(record.Patient?.User?.FullName ?? "patient");
            var fileName = $"Prescription_{patientName}_{record.VisitDateUtc:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();

            return string.IsNullOrWhiteSpace(cleaned) ? "patient" : cleaned;
        }
    }
}

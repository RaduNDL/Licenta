using System;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.MedicalRecords
{
    [Authorize(Roles = "Doctor")]
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

            var pdfBytes = _pdf.GeneratePrescription(record);
            var fileName = $"Prescription_{(record.Patient?.User?.FullName ?? "patient")}_{record.VisitDateUtc:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}

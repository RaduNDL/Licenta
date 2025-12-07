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
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            var record = await _db.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null || record.DoctorId != doctor.Id)
                return NotFound();

            var pdfBytes = _pdf.GeneratePrescription(record);
            var fileName = $"Prescription_{record.Patient?.User?.FullName ?? "patient"}_{record.VisitDateUtc:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}

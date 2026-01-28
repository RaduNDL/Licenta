using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Attachments
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public Guid? PatientId { get; set; }
        public List<MedicalAttachment> Attachments { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid? patientId = null)
        {
            var doctorUser = await _userManager.GetUserAsync(User);
            if (doctorUser == null) return Unauthorized();

            var doctorProfile = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);
            if (doctorProfile == null) return Unauthorized();

            PatientId = patientId;

            var q = _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctorProfile.Id);

            if (!string.IsNullOrWhiteSpace(doctorUser.ClinicId))
            {
                q = q.Where(a => a.Patient != null && a.Patient.User != null && a.Patient.User.ClinicId == doctorUser.ClinicId);
            }

            if (patientId.HasValue && patientId.Value != Guid.Empty)
            {
                q = q.Where(a => a.PatientId == patientId.Value);
            }

            Attachments = await q
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            return Page();
        }
    }
}
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
    public class InboxModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public InboxModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<MedicalAttachment> Items { get; set; } = new();
        public Guid CurrentDoctorId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
                return Forbid();

            CurrentDoctorId = doctor.Id;

            var clinicId = (user.ClinicId ?? "").Trim();
            var hasClinic = !string.IsNullOrWhiteSpace(clinicId);

            Items = await _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User) // <- aici se rezolvă warning-ul de null
                .Where(a =>
                    a.Status == AttachmentStatus.Pending &&
                    (a.DoctorId == null || a.DoctorId == doctor.Id) &&
                    (!hasClinic ||
                        (a.Patient != null &&
                         a.Patient.User != null &&
                         (a.Patient.User.ClinicId ?? "").Trim() == clinicId)
                    )
                )
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostClaimAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
                return Forbid();

            var att = await _db.MedicalAttachments
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.Status == AttachmentStatus.Pending &&
                    a.DoctorId == null);

            if (att == null)
                return RedirectToPage();

            att.DoctorId = doctor.Id;
            att.AssignedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Document assigned to you.";
            return RedirectToPage("/Doctor/Attachments/Review", new { id = att.Id });
        }
    }
}

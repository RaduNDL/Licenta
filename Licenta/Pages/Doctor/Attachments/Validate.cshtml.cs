using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Attachments
{
    [Authorize(Roles = "Doctor")]
    public class ValidateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ValidateModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        public MedicalAttachment? Item { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null) return Forbid();

            Item = await LoadForDoctorAsync(user, doctor.Id, Id, ct);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null) return Forbid();

            var att = await LoadForDoctorAsync(user, doctor.Id, Id, ct);
            if (att == null) return RedirectToPage("/Doctor/Attachments/Inbox");

            if (att.Status != AttachmentStatus.Validated)
            {
                att.Status = AttachmentStatus.Validated;
                att.ValidatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return RedirectToPage("/Doctor/Predictions/FromAttachment", new { id = att.Id });
        }

        private async Task<MedicalAttachment?> LoadForDoctorAsync(ApplicationUser user, Guid doctorId, Guid attachmentId, CancellationToken ct)
        {
            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

            if (att == null) return null;

            if (!string.IsNullOrWhiteSpace(user.ClinicId))
            {
                if (att.Patient?.User?.ClinicId != user.ClinicId) return null;
            }

            if (att.DoctorId == null || att.DoctorId == Guid.Empty)
            {
                att.DoctorId = doctorId;
                att.AssignedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            if (att.DoctorId != doctorId) return null;

            return att;
        }
    }
}
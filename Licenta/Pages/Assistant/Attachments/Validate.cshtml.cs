using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Attachments
{
    [Authorize(Roles = "Assistant")]
    public class ValidateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ValidateModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<MedicalAttachment> Pending { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                Pending = new();
                return;
            }

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found for this user.";
                Pending = new();
                return;
            }

            Pending = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.Id && a.Status == AttachmentStatus.Pending)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync(Guid id, string action)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage();
            }

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found for this user.";
                return RedirectToPage();
            }

            var attachment = await _db.MedicalAttachments
                .Include(x => x.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(x => x.Id == id && x.DoctorId == doctor.Id);

            if (attachment == null)
            {
                TempData["StatusMessage"] = "Attachment not found.";
                return RedirectToPage();
            }

            if (action == "approve")
            {
                attachment.Status = AttachmentStatus.Validated;
                attachment.ValidatedAtUtc = DateTime.UtcNow;
                attachment.ValidatedByDoctorId = doctor.Id;
            }
            else
            {
                attachment.Status = AttachmentStatus.Rejected;
                attachment.ValidatedAtUtc = DateTime.UtcNow;
                attachment.ValidatedByDoctorId = doctor.Id;
            }

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"Attachment '{attachment.FileName}' marked as {attachment.Status}.";
            return RedirectToPage();
        }
    }
}

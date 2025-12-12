using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Attachments
{
    [Authorize(Roles = "Assistant")]
    public class ValidateModel : PageModel
    {
        private readonly AppDbContext _db;

        public ValidateModel(AppDbContext db)
        {
            _db = db;
        }

        public List<MedicalAttachment> Pending { get; set; } = new();

        public async Task OnGetAsync()
        {
            Pending = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.Status == AttachmentStatus.Pending && a.Type != "AppointmentRequest")
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync(Guid id, string action)
        {
            var attachment = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attachment == null)
            {
                TempData["StatusMessage"] = "Attachment not found.";
                return RedirectToPage();
            }

            if (attachment.Status != AttachmentStatus.Pending)
            {
                TempData["StatusMessage"] = "Attachment has already been processed.";
                return RedirectToPage();
            }

            if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
            {
                attachment.Status = AttachmentStatus.Validated;
            }
            else
            {
                attachment.Status = AttachmentStatus.Rejected;
            }

            attachment.ValidatedAtUtc = DateTime.UtcNow;

            if (attachment.DoctorId != Guid.Empty)
            {
                attachment.ValidatedByDoctorId = attachment.DoctorId;
            }

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"Attachment '{attachment.FileName}' marked as {attachment.Status}.";
            return RedirectToPage();
        }
    }
}

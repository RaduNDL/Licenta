using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Licenta.Pages.Files
{
    [Authorize]
    public class AttachmentModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAttachmentStorage _attachmentStorage;

        public AttachmentModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IAttachmentStorage attachmentStorage)
        {
            _db = db;
            _userManager = userManager;
            _attachmentStorage = attachmentStorage;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient)!.ThenInclude(p => p!.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (att == null) return NotFound();

            var hasAccess = false;

            if (att.Patient?.UserId == user.Id)
                hasAccess = true;

            if (!hasAccess)
            {
                var doctor = await _db.Doctors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.UserId == user.Id);

                if (doctor != null)
                {
                    if (att.DoctorId == doctor.Id || att.ValidatedByDoctorId == doctor.Id)
                        hasAccess = true;

                    if (att.DoctorId == null &&
                        !string.IsNullOrWhiteSpace(user.ClinicId) &&
                        att.Patient?.User?.ClinicId == user.ClinicId)
                        hasAccess = true;
                }
            }

            if (!hasAccess) return Forbid();

            var normalized = _attachmentStorage.NormalizeLegacyPath(att.FilePath);
            if (string.IsNullOrWhiteSpace(normalized))
                return NotFound();

            if (!_attachmentStorage.Exists(normalized))
                return NotFound("Attachment file is missing on disk.");

            Stream stream;
            try
            {
                stream = _attachmentStorage.OpenRead(normalized);
            }
            catch
            {
                return BadRequest("Invalid attachment path.");
            }

            return File(
                stream,
                att.ContentType ?? "application/octet-stream",
                att.FileName ?? "file");
        }
    }
}
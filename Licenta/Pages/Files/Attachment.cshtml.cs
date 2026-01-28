using Licenta.Areas.Identity.Data;
using Licenta.Models;
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

        public AttachmentModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (att == null) return NotFound();

            var hasAccess = false;

            if (att.Patient?.UserId == user.Id)
                hasAccess = true;

            if (!hasAccess)
            {
                var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
                if (doctor != null)
                {
                    if (att.DoctorId == doctor.Id ||
                        att.ValidatedByDoctorId == doctor.Id)
                        hasAccess = true;

                    if (att.DoctorId == null &&
                        !string.IsNullOrWhiteSpace(user.ClinicId) &&
                        att.Patient?.User?.ClinicId == user.ClinicId)
                        hasAccess = true;
                }
            }

            if (!hasAccess) return Forbid();

            if (!System.IO.File.Exists(att.FilePath))
                return NotFound();

            return PhysicalFile(
                att.FilePath,
                att.ContentType ?? "application/octet-stream",
                att.FileName);
        }
    }
}

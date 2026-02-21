using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Attachments
{
    [Authorize(Roles = "Patient")]
    public class DetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        public MedicalAttachment? Item { get; private set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Forbid();

            var userId = user.Id;

            Item = await _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Patient!.User)
                .Include(a => a.Doctor)
                .Include(a => a.Doctor!.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.Patient != null && a.Patient.UserId == userId);

            if (Item == null)
                return NotFound();

            return Page();
        }
    }
}

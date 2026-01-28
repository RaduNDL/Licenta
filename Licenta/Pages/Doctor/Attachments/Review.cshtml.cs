using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Attachments
{
    [Authorize(Roles = "Doctor")]
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public MedicalAttachment Item { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel { public string DoctorNotes { get; set; } }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            Item = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (Item == null) return NotFound();

            Input.DoctorNotes = Item.DoctorNotes;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id, string action)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            var item = await _db.MedicalAttachments.FindAsync(id);
            if (item == null) return NotFound();

            item.DoctorNotes = Input.DoctorNotes;
            item.DoctorId = doctor.Id;
            item.ValidatedByDoctorId = doctor.Id;
            item.ValidatedAtUtc = DateTime.UtcNow;

            if (action == "ai")
            {
                item.Status = AttachmentStatus.Validated;
                await _db.SaveChangesAsync();
                return RedirectToPage("/Doctor/Predictions/Record", new { id = item.Id });
            }
            else if (action == "validate")
            {
                item.Status = AttachmentStatus.Validated;
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = "Document validated manually. Patient can now see the results.";
                return RedirectToPage("/Doctor/Attachments/Inbox");
            }
            else if (action == "reject")
            {
                item.Status = AttachmentStatus.Rejected;
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = "Document rejected.";
                return RedirectToPage("/Doctor/Attachments/Inbox");
            }

            return Page();
        }
    }
}
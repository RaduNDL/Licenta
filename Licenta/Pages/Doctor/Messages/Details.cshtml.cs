using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Messages
{
    [Authorize(Roles = "Doctor")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public InternalMessage Message { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            Message = await _db.InternalMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Message == null || (Message.RecipientId != currentUser.Id && Message.SenderId != currentUser.Id))
            {
                return NotFound();
            }

            return Page();
        }
    }
}

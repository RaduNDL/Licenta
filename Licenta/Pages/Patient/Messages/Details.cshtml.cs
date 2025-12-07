using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Messages
{
    [Authorize(Roles = "Patient")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public InternalMessage Message { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var message = await _db.InternalMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null || (message.RecipientId != currentUser.Id && message.SenderId != currentUser.Id))
            {
                return NotFound();
            }

            Message = message;
            return Page();
        }
    }
}

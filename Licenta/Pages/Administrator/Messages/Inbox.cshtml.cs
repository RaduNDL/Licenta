using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Administrator.Messages
{
    [Authorize(Roles = "Administrator")]
    public class InboxModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public InboxModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<InternalMessage> Messages { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            Messages = await _db.InternalMessages
                .Include(m => m.Sender)
                .Where(m => m.RecipientId == user.Id)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();
        }
    }
}

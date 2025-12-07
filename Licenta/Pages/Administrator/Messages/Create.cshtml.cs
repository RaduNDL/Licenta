using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Administrator.Messages
{
    [Authorize(Roles = "Administrator")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public class InputModel
        {
            public string RecipientId { get; set; } = null!;
            public string Subject { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList Recipients { get; set; } = default!;

        public async Task OnGetAsync(string? userId)
        {
            await LoadRecipientsAsync(userId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadRecipientsAsync(Input.RecipientId);
                return Page();
            }

            var sender = await _userManager.GetUserAsync(User);
            if (sender == null)
            {
                return Unauthorized();
            }

            var message = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                RecipientId = Input.RecipientId,
                Subject = Input.Subject,
                Body = Input.Body,
                SentAt = DateTime.UtcNow
            };

            _db.InternalMessages.Add(message);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Administrator/Messages/Inbox");
        }

        private async Task LoadRecipientsAsync(string? preselectUserId)
        {
            var users = await _db.Users
                .OrderBy(u => u.FullName ?? u.Email)
                .ToListAsync();

            Recipients = new SelectList(
                users,
                nameof(ApplicationUser.Id),
                nameof(ApplicationUser.FullName),
                preselectUserId
            );
        }
    }
}

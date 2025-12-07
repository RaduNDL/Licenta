using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Messages
{
    [Authorize(Roles = "Doctor")]
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
            public string RecipientId { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList Recipients { get; set; }

        public async Task OnGetAsync(string? patientId)
        {
            await LoadRecipientsAsync(patientId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadRecipientsAsync(Input.RecipientId);
                return Page();
            }

            var sender = await _userManager.GetUserAsync(User);

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

            return RedirectToPage("/Doctor/Messages/Inbox");
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

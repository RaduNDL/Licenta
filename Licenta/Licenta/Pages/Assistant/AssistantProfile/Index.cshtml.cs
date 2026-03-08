using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.AssistantProfile
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [StringLength(120)]
            public string FullName { get; set; } = "";

            [EmailAddress]
            public string Email { get; set; } = "";

            [StringLength(64)]
            public string? ClinicId { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            Input = new InputModel
            {
                FullName = user.FullName ?? "",
                Email = user.Email ?? user.UserName ?? "",
                ClinicId = user.ClinicId
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            Input.Email = user.Email ?? user.UserName ?? "";

            if (!ModelState.IsValid)
                return Page();

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == user.Id);
            if (u == null)
                return Challenge();

            u.FullName = (Input.FullName ?? "").Trim();
            u.ClinicId = string.IsNullOrWhiteSpace(Input.ClinicId) ? null : Input.ClinicId.Trim();

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Profile updated.";
            return RedirectToPage();
        }
    }
}

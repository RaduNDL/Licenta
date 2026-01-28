using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.AdminProfile
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public string FullName { get; private set; } = "Administrator";
        public string Email { get; private set; } = "";
        public string UserName { get; private set; } = "";
        public string? ClinicName { get; private set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public sealed class InputModel
        {
            [Required]
            [StringLength(120)]
            [Display(Name = "Full name")]
            public string FullName { get; set; } = "";

            [StringLength(40)]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var changed = false;

            if (user.FullName != Input.FullName)
            {
                user.FullName = Input.FullName;
                changed = true;
            }

            if (user.PhoneNumber != Input.PhoneNumber)
            {
                user.PhoneNumber = Input.PhoneNumber;
                changed = true;
            }

            if (changed)
            {
                var res = await _userManager.UpdateAsync(user);
                if (!res.Succeeded)
                {
                    foreach (var e in res.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);

                    await LoadAsync(user);
                    return Page();
                }

                TempData["StatusMessage"] = "Profile updated successfully.";
            }
            else
            {
                TempData["StatusMessage"] = "No changes to save.";
            }

            return RedirectToPage();
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            FullName = user.FullName ?? user.UserName ?? "Administrator";
            Email = user.Email ?? "";
            UserName = user.UserName ?? "";

            Input = new InputModel
            {
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber
            };

            var settings = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ClinicName = settings?.ClinicName;
        }
    }
}

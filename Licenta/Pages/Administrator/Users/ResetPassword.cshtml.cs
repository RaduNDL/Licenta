using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public string UserId { get; set; } = string.Empty;

        public string? TargetEmail { get; set; }

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return RedirectToPage("./Index");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage("./Index");
            }

            UserId = user.Id;
            TargetEmail = user.Email ?? user.UserName;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, NewPassword);

            if (!reset.Succeeded)
            {
                ErrorMessage = string.Join("; ", reset.Errors.Select(e => e.Description));
                return Page();
            }

            SuccessMessage = "Password successfully reset.";
            return Page();
        }
    }
}

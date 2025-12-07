using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class EditModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public EditModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string Id { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Display(Name = "Full name")]
            [MaxLength(256)]
            public string? FullName { get; set; }

            [Display(Name = "Clinic Id")]
            [MaxLength(64)]
            public string? ClinicId { get; set; }

            [Display(Name = "Email confirmed")]
            public bool EmailConfirmed { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["StatusMessage"] = "Invalid user id.";
                return RedirectToPage("./Index");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage("./Index");
            }

            Input = new InputModel
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                FullName = user.FullName, 
                ClinicId = user.ClinicId,
                EmailConfirmed = user.EmailConfirmed
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByIdAsync(Input.Id);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage("./Index");
            }

            var emailToSet = Input.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(emailToSet);
            if (existing != null && existing.Id != user.Id)
            {
                ModelState.AddModelError("Input.Email", "Another user with this email already exists.");
                return Page();
            }

            user.Email = emailToSet;
            user.UserName = emailToSet;
            user.FullName = Input.FullName?.Trim() ?? string.Empty; 
            user.ClinicId = string.IsNullOrWhiteSpace(Input.ClinicId) ? null : Input.ClinicId!.Trim();
            user.EmailConfirmed = Input.EmailConfirmed;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }

            TempData["StatusMessage"] = "User updated successfully.";
            return RedirectToPage("./Index");
        }
    }
}

using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class DeleteUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private const bool PROTECT_DOCTOR = false;

        public DeleteUserModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [BindProperty] public string UserId { get; set; } = string.Empty;
        public string TargetEmail { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["StatusMessage"] = "Invalid request.";
                return RedirectToPage("./Index");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage("./Index");
            }

            UserId = user.Id;
            TargetEmail = user.Email ?? user.UserName ?? "(no email)";
            Roles = (await _userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                TempData["StatusMessage"] = "Invalid request.";
                return RedirectToPage("./Index");
            }

            var user = await _userManager.FindByIdAsync(UserId);
            if (user is null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage("./Index");
            }

            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["StatusMessage"] = "You cannot delete your own account.";
                return RedirectToPage("./Index");
            }

            var roles = await _userManager.GetRolesAsync(user);

            // Protecție: nu lăsa sistemul fără niciun Administrator activ
            if (roles.Contains("Administrator"))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                var otherActiveAdmins = admins
                    .Where(a => a.Id != user.Id)
                    .Count(a => !a.LockoutEnd.HasValue || a.LockoutEnd <= DateTimeOffset.UtcNow);

                if (otherActiveAdmins == 0)
                {
                    TempData["StatusMessage"] = "Cannot delete the last active Administrator account.";
                    return RedirectToPage("./Index");
                }
            }

            // Protejează doctorii dacă PROTECT_DOCTOR = true
            if (PROTECT_DOCTOR && roles.Contains("Doctor"))
            {
                TempData["StatusMessage"] = "Doctors cannot be deleted directly.";
                return RedirectToPage("./Index");
            }

            // Soft delete instead of hard delete
            user.IsSoftDeleted = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = result.Succeeded
                ? $"User {user.Email ?? user.UserName} has been deactivated."
                : $"Error deactivating user: {string.Join("; ", result.Errors.Select(e => e.Description))}";

            return RedirectToPage("./Index");
        }
    }
}

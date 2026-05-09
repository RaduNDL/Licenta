using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class DeleteUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _db;

        private const bool PROTECT_DOCTOR = false;

        public DeleteUserModel(UserManager<ApplicationUser> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
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

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id);

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

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == UserId);

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

            if (roles.Contains("Administrator"))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                var otherActiveAdmins = admins
                    .Where(a => a.Id != user.Id)
                    .Count(a => !IsLocked(a) && !a.IsSoftDeleted);

                if (otherActiveAdmins == 0)
                {
                    TempData["StatusMessage"] = "Cannot deactivate the last active Administrator account.";
                    return RedirectToPage("./Index");
                }
            }

            if (PROTECT_DOCTOR && roles.Contains("Doctor"))
            {
                TempData["StatusMessage"] = "Doctors cannot be deleted directly.";
                return RedirectToPage("./Index");
            }

            user.IsSoftDeleted = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"User {user.Email ?? user.UserName} has been deactivated.";
            return RedirectToPage("./Index");
        }

        private static bool IsLocked(ApplicationUser user)
            => user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
    }
}
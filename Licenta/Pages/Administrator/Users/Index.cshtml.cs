using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private const bool PROTECT_DOCTOR_FROM_LOCK = true;

        public IndexModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public List<ApplicationUser> Users { get; private set; } = new();
        public Dictionary<string, List<string>> RolesByUserId { get; private set; } = new();
        public string? CurrentUserId { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            CurrentUserId = _userManager.GetUserId(User);

            Users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(u => u.Email ?? u.UserName)
                .ToListAsync();

            RolesByUserId = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var u in Users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                RolesByUserId[u.Id] = roles.OrderBy(r => r).ToList();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(u => u.Email ?? u.UserName)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,Email,FullName,Roles,IsLocked");

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var isLocked = IsLocked(u);

                var line = string.Join(",",
                    Quote(u.Id),
                    Quote(u.Email ?? u.UserName ?? string.Empty),
                    Quote(u.FullName ?? string.Empty),
                    Quote(string.Join(";", roles)),
                    Quote(isLocked ? "1" : "0"));

                sb.AppendLine(line);
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "users.csv");
        }

        private static string Quote(string? s)
            => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";

        public async Task<IActionResult> OnPostToggleLockAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["StatusMessage"] = "Invalid user id.";
                return RedirectToPage();
            }

            var target = await _userManager.FindByIdAsync(id);
            if (target is null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage();
            }

            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(target.Id, currentUserId, StringComparison.Ordinal))
            {
                TempData["StatusMessage"] = "You cannot change lock status of your own account.";
                return RedirectToPage();
            }

            var roles = await _userManager.GetRolesAsync(target);

            if (PROTECT_DOCTOR_FROM_LOCK && roles.Contains("Doctor") && !IsLocked(target))
            {
                TempData["StatusMessage"] = "Doctors are protected from locking.";
                return RedirectToPage();
            }

            if (roles.Contains("Administrator") && !IsLocked(target))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                var otherActiveAdmins = admins
                    .Where(a => a.Id != target.Id)
                    .Count(a => !IsLocked(a) && !a.IsSoftDeleted);

                if (otherActiveAdmins == 0)
                {
                    TempData["StatusMessage"] = "System must keep at least one active Administrator.";
                    return RedirectToPage();
                }
            }

            var locked = IsLocked(target);
            var (_, message) = await TryChangeLockAsync(target.Id, shouldLock: !locked);
            TempData["StatusMessage"] = message;
            return RedirectToPage();
        }

        private static bool IsLocked(ApplicationUser user)
            => user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        private async Task<(bool ok, string message)> TryChangeLockAsync(string userId, bool shouldLock)
        {
            var target = await _userManager.FindByIdAsync(userId);
            if (target is null)
                return (false, "User not found.");

            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(target.Id, currentUserId, StringComparison.Ordinal))
                return (false, "You cannot change lock status of your own account.");

            var roles = await _userManager.GetRolesAsync(target);

            if (PROTECT_DOCTOR_FROM_LOCK && roles.Contains("Doctor") && shouldLock)
                return (false, "Doctors are protected from locking.");

            if (roles.Contains("Administrator") && shouldLock)
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                var otherActiveAdmins = admins
                    .Where(a => a.Id != target.Id)
                    .Count(a => !IsLocked(a) && !a.IsSoftDeleted);

                if (otherActiveAdmins == 0)
                    return (false, "System must keep at least one active Administrator.");
            }

            if (shouldLock)
            {
                target.LockoutEnabled = true;
                target.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                var res = await _userManager.UpdateAsync(target);
                return res.Succeeded
                    ? (true, $"User {target.Email ?? target.UserName} blocked.")
                    : (false, string.Join("; ", res.Errors.Select(e => e.Description)));
            }

            target.LockoutEnd = null;
            target.AccessFailedCount = 0;
            var res2 = await _userManager.UpdateAsync(target);
            return res2.Succeeded
                ? (true, $"User {target.Email ?? target.UserName} unblocked.")
                : (false, string.Join("; ", res2.Errors.Select(e => e.Description)));
        }
    }
}

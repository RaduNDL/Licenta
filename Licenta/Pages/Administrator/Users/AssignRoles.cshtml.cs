using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class AssignRolesModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AssignRolesModel> _logger;
        private readonly AppDbContext _db;

        private static readonly string[] AllowedRoles = new[]
        {
            "Administrator", "Doctor", "Assistant", "Patient"
        };

        public AssignRolesModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AssignRolesModel> logger,
            AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _db = db;
        }

        [BindProperty] public string UserId { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public List<string> AllRoles { get; set; } = new();
        [BindProperty] public List<string> SelectedRoles { get; set; } = new();
        public string? Error { get; set; }
        public string? Status { get; set; }
        public bool IsSelf { get; set; }

        public async Task<IActionResult> OnGet(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Status"] = "Invalid user ID.";
                return RedirectToPage("Index");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Status"] = "User not found.";
                return RedirectToPage("Index");
            }

            UserId = user.Id;
            UserEmail = user.Email ?? user.UserName;
            IsSelf = string.Equals(UserId, _userManager.GetUserId(User), StringComparison.Ordinal);

            AllRoles = _roleManager.Roles
                .Select(r => r.Name!)
                .Where(n => AllowedRoles.Contains(n))
                .OrderBy(n => n)
                .ToList();

            SelectedRoles = (await _userManager.GetRolesAsync(user))
                .Where(n => AllowedRoles.Contains(n))
                .OrderBy(n => n)
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            if (!ModelState.IsValid)
                return await OnGet(UserId);

            var targetUser = await _userManager.FindByIdAsync(UserId);
            if (targetUser == null)
            {
                TempData["Status"] = "User not found.";
                return RedirectToPage("Index");
            }

            SelectedRoles = (SelectedRoles ?? new())
                .Where(r => AllowedRoles.Contains(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = await _userManager.GetRolesAsync(targetUser);

            var isSelf = string.Equals(targetUser.Id, _userManager.GetUserId(User), StringComparison.Ordinal);
            var removingAdminFromSelf = isSelf &&
                current.Contains("Administrator") &&
                !SelectedRoles.Contains("Administrator");

            if (removingAdminFromSelf)
            {
                Error = "You cannot remove your own Administrator role.";
                return await OnGet(UserId);
            }

            var isRemovingAdmin = current.Contains("Administrator") &&
                                  !SelectedRoles.Contains("Administrator");
            if (isRemovingAdmin)
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                if (admins.Count == 1 && admins[0].Id == targetUser.Id)
                {
                    Error = "At least one Administrator is required.";
                    return await OnGet(UserId);
                }
            }

            foreach (var role in SelectedRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    var created = await _roleManager.CreateAsync(new IdentityRole(role));
                    if (!created.Succeeded)
                    {
                        Error = $"Failed to create role '{role}'.";
                        return await OnGet(UserId);
                    }
                }
            }

            var toAdd = SelectedRoles.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
            var toRemove = current.Except(SelectedRoles, StringComparer.OrdinalIgnoreCase).ToArray();

            if (toAdd.Length > 0)
            {
                var addRes = await _userManager.AddToRolesAsync(targetUser, toAdd);
                if (!addRes.Succeeded)
                {
                    Error = string.Join("; ", addRes.Errors.Select(e => e.Description));
                    return await OnGet(UserId);
                }
            }

            if (toRemove.Length > 0)
            {
                var remRes = await _userManager.RemoveFromRolesAsync(targetUser, toRemove);
                if (!remRes.Succeeded)
                {
                    Error = string.Join("; ", remRes.Errors.Select(e => e.Description));
                    return await OnGet(UserId);
                }
            }

            // 🔹 CREĂM PROFILURI dacă userul a primit Patient / Doctor
            await EnsureProfilesForRolesAsync(targetUser, SelectedRoles);

            _logger.LogInformation("Updated roles for user {UserId}. Added: {Add}; Removed: {Remove}",
                targetUser.Id, string.Join(",", toAdd), string.Join(",", toRemove));

            Status = "Roles successfully updated.";
            TempData["Status"] = Status;
            return RedirectToPage("Index");
        }

        private async Task EnsureProfilesForRolesAsync(ApplicationUser user, IEnumerable<string> roles)
        {
            if (roles.Contains("Patient"))
            {
                if (!await _db.Patients.AnyAsync(p => p.UserId == user.Id))
                {
                    _db.Patients.Add(new PatientProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id
                    });
                }
            }

            if (roles.Contains("Doctor"))
            {
                if (!await _db.Doctors.AnyAsync(d => d.UserId == user.Id))
                {
                    _db.Doctors.Add(new DoctorProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}

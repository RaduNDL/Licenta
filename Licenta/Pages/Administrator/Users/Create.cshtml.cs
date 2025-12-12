using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public CreateModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public List<string> Roles { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, MinLength(6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required]
            public string Role { get; set; } = "Patient";

            public bool EmailConfirmed { get; set; } = true;
        }

        public void OnGet()
        {
            Roles = _roleManager.Roles
                .Select(r => r.Name!)
                .OrderBy(r => r)
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Roles = _roleManager.Roles
                .Select(r => r.Name!)
                .OrderBy(r => r)
                .ToList();

            if (!ModelState.IsValid)
                return Page();

            if (!await _roleManager.RoleExistsAsync(Input.Role))
            {
                ModelState.AddModelError(string.Empty, $"Role '{Input.Role}' does not exist.");
                return Page();
            }

            var email = Input.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                ModelState.AddModelError("Input.Email", "A user with this email already exists.");
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = Input.EmailConfirmed
            };

            var create = await _userManager.CreateAsync(user, Input.Password);
            if (!create.Succeeded)
            {
                foreach (var e in create.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            if (string.IsNullOrEmpty(user.ClinicId))
            {
                user.ClinicId = GenerateClinicId();
                await _userManager.UpdateAsync(user);
            }

            await _userManager.AddToRoleAsync(user, Input.Role);

            TempData["StatusMessage"] = $"User {Input.Email} created with role '{Input.Role}'.";
            return RedirectToPage("./Index");
        }

        private static string GenerateClinicId()
        {
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            return $"CL-{guid}";
        }
    }
}
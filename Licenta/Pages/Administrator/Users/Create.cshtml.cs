using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.Users
{
    [Authorize(Roles = "Administrator")]
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;

        public CreateModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        public List<string> Roles { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [MaxLength(256)]
            public string? FullName { get; set; }

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

            var email = (Input.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("Input.Email", "Email is required.");
                return Page();
            }

            if (!await _roleManager.RoleExistsAsync(Input.Role))
            {
                ModelState.AddModelError(string.Empty, $"Role '{Input.Role}' does not exist.");
                return Page();
            }

            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                ModelState.AddModelError(string.Empty, "Admin user not found.");
                return Page();
            }

            var adminClinicId = (admin.ClinicId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(adminClinicId))
            {
                ModelState.AddModelError(string.Empty, "Your admin account is not linked to a clinic.");
                return Page();
            }

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
                EmailConfirmed = Input.EmailConfirmed,
                ClinicId = adminClinicId,
                FullName = string.IsNullOrWhiteSpace(Input.FullName) ? null : Input.FullName.Trim()
            };

            var strategy = _db.Database.CreateExecutionStrategy();

            IdentityResult? createResult = null;
            IdentityResult? addRoleResult = null;
            string? errorMessage = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    createResult = await _userManager.CreateAsync(user, Input.Password);
                    if (!createResult.Succeeded)
                    {
                        await tx.RollbackAsync();
                        return;
                    }

                    addRoleResult = await _userManager.AddToRoleAsync(user, Input.Role);
                    if (!addRoleResult.Succeeded)
                    {
                        await _userManager.DeleteAsync(user);
                        await tx.RollbackAsync();
                        return;
                    }

                    if (Input.Role == "Doctor")
                    {
                        _db.Doctors.Add(new DoctorProfile
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            Specialty = "General Practice",
                            ProfileImagePath = "/images/default.jpg"
                        });
                    }
                    else if (Input.Role == "Patient")
                    {
                        _db.Patients.Add(new PatientProfile
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id
                        });
                    }

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            if (createResult != null && !createResult.Succeeded)
            {
                foreach (var e in createResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            if (addRoleResult != null && !addRoleResult.Succeeded)
            {
                foreach (var e in addRoleResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                ModelState.AddModelError(string.Empty, errorMessage);
                return Page();
            }

            TempData["StatusMessage"] = $"User {email} created with role '{Input.Role}'.";
            return RedirectToPage("./Index");
        }
    }
}
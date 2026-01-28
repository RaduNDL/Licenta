#nullable disable

using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;
        private readonly INotificationService _notifier;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db,
            INotificationService notifier)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _roleManager = roleManager;
            _db = db;
            _notifier = notifier;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(80, MinimumLength = 2)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            return Task.CompletedTask;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            var email = (Input.Email ?? "").Trim();
            var fullName = (Input.FullName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError(string.Empty, "Full Name is required.");
                return Page();
            }

            var user = CreateUser();
            user.FullName = fullName;

            await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, email, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(user.ClinicId))
                user.ClinicId = await ResolveClinicIdAsync();

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }

            _logger.LogInformation("User created a new account with password.");

            const string defaultRole = "Patient";
            if (!await _roleManager.RoleExistsAsync(defaultRole))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(defaultRole));
                if (!roleResult.Succeeded)
                {
                    foreach (var e in roleResult.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);
                    return Page();
                }
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, defaultRole);
            if (!addRoleResult.Succeeded)
            {
                foreach (var e in addRoleResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            if (!await _db.Patients.AnyAsync(p => p.UserId == user.Id))
            {
                _db.Patients.Add(new PatientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id
                });
                await _db.SaveChangesAsync();
            }

            var admins = await _userManager.GetUsersInRoleAsync("Administrator");
            var displayName = user.FullName ?? user.Email ?? user.UserName ?? "Unknown user";
            var registeredAt = DateTime.Now;

            foreach (var admin in admins)
            {
                await _notifier.NotifyAsync(
                    admin,
                    NotificationType.System,
                    "New user registered",
                    $"User <b>{displayName}</b> registered at {registeredAt:f}.",
                    relatedEntity: "User",
                    relatedEntityId: user.Id
                );
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(ReturnUrl);
        }

        private async Task<string> ResolveClinicIdAsync()
        {
            var any = await _db.Users.AsNoTracking()
                .Where(x => x.ClinicId != null && x.ClinicId != "")
                .Select(x => x.ClinicId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(any))
                return any.Trim();

            return GenerateClinicId();
        }

        private static string GenerateClinicId()
        {
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            return $"CL-{guid}";
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor.");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("The default UI requires a user store with email support.");

            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}

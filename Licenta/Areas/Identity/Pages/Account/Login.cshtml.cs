#nullable disable

using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Licenta.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            INotificationService notifier,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _notifier = notifier;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                ModelState.AddModelError(string.Empty, ErrorMessage);

            ReturnUrl = returnUrl ?? Url.Content("~/");
            await Task.CompletedTask;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");

                var loggedInUser = await _userManager.FindByEmailAsync(Input.Email);
                if (loggedInUser != null)
                {
                    var admins = await _userManager.GetUsersInRoleAsync("Administrator");

                    var displayName = loggedInUser.FullName
                        ?? loggedInUser.Email
                        ?? loggedInUser.UserName
                        ?? "Unknown user";

                    var loginTimeLocal = DateTime.Now;

                    foreach (var admin in admins)
                    {
                        await _notifier.NotifyAsync(
                            admin,
                            NotificationType.System,
                            "User signed in",
                            $"User <b>{displayName}</b> signed in at {loginTimeLocal:f}.",
                            relatedEntity: "User",
                            relatedEntityId: loggedInUser.Id
                        );
                    }
                }

                if (!string.IsNullOrEmpty(ReturnUrl)
                    && Url.IsLocalUrl(ReturnUrl)
                    && !ReturnUrl.Contains("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase))
                {
                    return LocalRedirect(ReturnUrl);
                }

                return Redirect("~/");
            }

            if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(string.Empty, "Two-factor authentication is not enabled in this build.");
                return Page();
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "This account is locked out. Please contact an administrator.");
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}

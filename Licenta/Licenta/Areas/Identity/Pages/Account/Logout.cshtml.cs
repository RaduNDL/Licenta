#nullable disable

using System;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Licenta.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            INotificationService notifier,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _notifier = notifier;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            var loggedOutUser = await _userManager.GetUserAsync(User);

            if (loggedOutUser != null)
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");

                var displayName = loggedOutUser.FullName
                    ?? loggedOutUser.Email
                    ?? loggedOutUser.UserName
                    ?? "Unknown user";

                var logoutTimeLocal = DateTime.Now;

                foreach (var admin in admins)
                {
                    await _notifier.NotifyAsync(
                        admin,
                        NotificationType.System,
                        "User signed out",
                        $"User <b>{displayName}</b> signed out at {logoutTimeLocal:f}.",
                        relatedEntity: "User",
                        relatedEntityId: loggedOutUser.Id
                    );
                }
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToPage("/Index", new { area = "" });
        }
    }
}

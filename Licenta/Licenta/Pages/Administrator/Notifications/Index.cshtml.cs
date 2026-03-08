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

namespace Licenta.Pages.Administrator.Notifications
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public sealed class NotificationItemVm
        {
            public DateTime When { get; set; }
            public string Type { get; set; } = "";
            public string Title { get; set; } = "";
            public string Message { get; set; } = "";
            public bool IsRead { get; set; }
        }

        public List<NotificationItemVm> Items { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var sinceUtc = DateTime.UtcNow.AddDays(-30);

            Items = await _db.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == userId && n.CreatedAtUtc >= sinceUtc)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new NotificationItemVm
                {
                    When = n.CreatedAtUtc.ToLocalTime(),
                    Type = n.Type.ToString(),
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var notifier = HttpContext.RequestServices.GetService(typeof(Licenta.Services.INotificationService)) as Licenta.Services.INotificationService;
            if (notifier != null)
            {
                await notifier.MarkAllReadAsync(user.Id);
            }
            else
            {
                var list = await _db.UserNotifications
                    .Where(x => x.UserId == user.Id && !x.IsRead)
                    .ToListAsync();

                foreach (var n in list)
                    n.IsRead = true;

                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "All notifications marked as read.";
            return RedirectToPage();
        }
    }
}

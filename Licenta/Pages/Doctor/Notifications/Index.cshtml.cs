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

namespace Licenta.Pages.Doctor.Notifications
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public class NotificationVm
        {
            public long Id { get; set; }
            public NotificationType Type { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool IsRead { get; set; }
            public DateTime When { get; set; }
            public string? RelatedEntity { get; set; }
            public string? RelatedEntityId { get; set; }
        }

        public List<NotificationVm> Items { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fromUtc = DateTime.UtcNow.AddDays(-30);

            Items = await _db.UserNotifications
                .Where(n => n.UserId == user.Id && n.CreatedAtUtc >= fromUtc)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new NotificationVm
                {
                    Id = n.Id,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    When = n.CreatedAtUtc,
                    RelatedEntity = n.RelatedEntity,
                    RelatedEntityId = n.RelatedEntityId
                })
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifier = HttpContext.RequestServices.GetService(typeof(Licenta.Services.INotificationService)) as Licenta.Services.INotificationService;
            if (notifier != null)
                await notifier.MarkAllReadAsync(user.Id);
            else
            {
                var list = await _db.UserNotifications
                    .Where(x => x.UserId == user.Id && !x.IsRead)
                    .ToListAsync();

                if (list.Count > 0)
                {
                    foreach (var n in list)
                        n.IsRead = true;

                    await _db.SaveChangesAsync();
                }
            }

            TempData["StatusMessage"] = "All notifications marked as read.";
            return RedirectToPage();
        }

    }
}

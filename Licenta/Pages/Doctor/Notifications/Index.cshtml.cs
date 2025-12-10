using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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

        public record NotificationVm(
            long Id,
            string Title,
            string Message,
            NotificationType Type,
            DateTime When,
            bool IsRead);

        public List<NotificationVm> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                Items = new();
                return;
            }

            var since = DateTime.UtcNow.AddDays(-30);

            Items = await _db.UserNotifications
                .Where(n => n.UserId == user.Id && n.CreatedAtUtc >= since)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new NotificationVm(
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.CreatedAtUtc.ToLocalTime(),
                    n.IsRead))
                .ToListAsync();
        }

        public async Task OnPostMarkAllReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var items = await _db.UserNotifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .ToListAsync();

            foreach (var n in items)
            {
                n.IsRead = true;
            }

            await _db.SaveChangesAsync();
            await OnGetAsync();
        }
    }
}

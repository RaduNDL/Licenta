using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient
{
    [Authorize(Roles = "Patient")]
    public class NotificationsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<UserNotification> Items { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            Items = await _db.UserNotifications
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(200)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostMarkReadAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var n = await _db.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (n == null) return RedirectToPage();

            if (!n.IsRead)
            {
                n.IsRead = true;
                n.ReadAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var list = await _db.UserNotifications
                .Where(x => x.UserId == user.Id && !x.IsRead)
                .ToListAsync();

            if (list.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var n in list)
                {
                    n.IsRead = true;
                    n.ReadAtUtc = now;
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
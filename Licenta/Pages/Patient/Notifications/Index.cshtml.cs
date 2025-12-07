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

namespace Licenta.Pages.Patient.Notifications
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public record NotificationVm(string Title, string Message, DateTime When);

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

            var docs = await _db.MedicalAttachments
                .Include(a => a.Patient)
                .Where(a =>
                    a.Patient != null &&
                    a.Patient.UserId == user.Id &&
                    a.Status != AttachmentStatus.Pending &&
                    (a.ValidatedAtUtc ?? a.UploadedAt) >= since)
                .OrderByDescending(a => a.ValidatedAtUtc ?? a.UploadedAt)
                .ToListAsync();

            foreach (var a in docs)
            {
                var when = a.ValidatedAtUtc ?? a.UploadedAt;

                string title = a.Status switch
                {
                    AttachmentStatus.Validated => "Document validated",
                    AttachmentStatus.Rejected => "Document rejected",
                    _ => $"Update: {a.Status}"
                };

                var fileLabel = string.IsNullOrWhiteSpace(a.Type)
                    ? a.FileName
                    : $"{a.FileName} ({a.Type})";

                var msg = fileLabel;
                if (!string.IsNullOrWhiteSpace(a.ValidationNotes))
                    msg += $": {a.ValidationNotes}";

                Items.Add(new NotificationVm(title, msg, when));
            }
        }
    }
}

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

namespace Licenta.Pages.Patient.Visits
{
    [Authorize(Roles = "Patient")]
    public class HistoryModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public HistoryModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public record GroupVm(DateTime Date, List<MedicalAttachment> Items);
        public List<GroupVm> Groups { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                Groups = new();
                return;
            }

            var docs = await _db.MedicalAttachments
                .Include(a => a.Patient)
                .Where(a =>
                    a.Patient != null &&
                    a.Patient.UserId == user.Id &&
                    a.Status == AttachmentStatus.Validated)
                .OrderByDescending(a => a.ValidatedAtUtc ?? a.UploadedAt)
                .ToListAsync();

            Groups = docs
                .GroupBy(a => (a.ValidatedAtUtc ?? a.UploadedAt).Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new GroupVm(g.Key, g.ToList()))
                .ToList();
        }
    }
}
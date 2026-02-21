using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Attachments
{
    [Authorize(Roles = "Patient")]
    public class IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        public List<MedicalAttachment> Items { get; private set; } = [];

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            var userId = user.Id;

            Items = await _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Patient!.User)
                .Where(a => a.Patient != null && a.Patient.UserId == userId)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }
    }
}

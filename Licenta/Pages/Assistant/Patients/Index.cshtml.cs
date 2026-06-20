using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Patients
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IList<PatientProfile> Patients { get; set; } = new List<PatientProfile>();
        public Dictionary<Guid, string?> PatientPhotos { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            Patients = await _db.Patients
                .Include(p => p.User)
                .Where(p => p.User.ClinicId == user!.ClinicId)
                .OrderBy(p => p.User.FullName)
                .ToListAsync();

            var patientIds = Patients.Select(p => p.Id).ToList();

            var latestPhotos = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => patientIds.Contains(a.PatientId) && a.Type == "ProfilePhoto")
                .GroupBy(a => a.PatientId)
                .Select(g => g.OrderByDescending(a => a.UploadedAt).FirstOrDefault())
                .ToListAsync();

            foreach (var p in Patients)
            {
                var photo = latestPhotos.FirstOrDefault(a => a != null && a.PatientId == p.Id);
                if (photo != null)
                {
                    var fp = (photo.FilePath ?? "").Trim().Replace("\\", "/");
                    if (!fp.StartsWith("/")) fp = "/" + fp;

                    PatientPhotos[p.Id] = fp.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
                        ? fp
                        : $"/Files/Attachment?id={photo.Id}";
                }
                else
                {
                    PatientPhotos[p.Id] = null;
                }
            }
        }
    }
}
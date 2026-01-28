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

namespace Licenta.Pages.Assistant.MedicalRecords
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

        public IList<MedicalRecord> ActiveRecords { get; set; } = new List<MedicalRecord>();
        public IList<MedicalRecord> CompletedRecords { get; set; } = new List<MedicalRecord>();

        public async Task OnGetAsync(Guid? patientId)
        {
            var user = await _userManager.GetUserAsync(User);
            var clinicId = user?.ClinicId;

            var baseQuery = _db.MedicalRecords
                .AsNoTracking()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                baseQuery = baseQuery.Where(r =>
                    r.Patient != null &&
                    r.Patient.User != null &&
                    r.Patient.User.ClinicId == clinicId);
            }

            if (patientId.HasValue)
            {
                baseQuery = baseQuery.Where(r => r.PatientId == patientId.Value);
            }

            CompletedRecords = await baseQuery
                .Where(r => r.Status == RecordStatus.Validated)
                .OrderByDescending(r => r.VisitDateUtc)
                .Take(250)
                .ToListAsync();

            ActiveRecords = await baseQuery
                .Where(r => r.Status != RecordStatus.Validated)
                .OrderByDescending(r => r.VisitDateUtc)
                .Take(250)
                .ToListAsync();
        }
    }
}

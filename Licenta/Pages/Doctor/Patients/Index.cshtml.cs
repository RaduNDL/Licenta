using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Patients
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

        public List<PatientProfile> Patients { get; set; } = new();

        public async Task OnGetAsync()
        {
            var doctorUser = await _userManager.GetUserAsync(User);
            if (doctorUser == null)
            {
                Patients = new();
                return;
            }

            var clinicId = (doctorUser.ClinicId ?? "").Trim();

            var patientRoleId = await _db.Roles
                .Where(r => r.Name == "Patient")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(patientRoleId))
            {
                Patients = new();
                return;
            }

            var patientUserIds = await _db.UserRoles
                .Where(ur => ur.RoleId == patientRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync();

            var query = _db.Patients
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.User != null && !p.User.IsSoftDeleted && patientUserIds.Contains(p.UserId));

            if (!string.IsNullOrWhiteSpace(clinicId))
                query = query.Where(p => (p.User!.ClinicId ?? "").Trim() == clinicId);

            Patients = await query
                .OrderBy(p => (p.User!.FullName ?? p.User.Email ?? p.User.UserName ?? ""))
                .ToListAsync();
        }
    }
}
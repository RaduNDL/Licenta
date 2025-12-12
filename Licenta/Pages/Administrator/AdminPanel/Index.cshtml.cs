using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Licenta.Pages.Administrator.AdminPanel
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int DoctorCount { get; set; }
        public int AssistantCount { get; set; }
        public int PatientCount { get; set; }

        public string? ClinicName { get; set; }
        public int MaxUploadMb { get; set; }

        public async Task OnGetAsync()
        {
            TotalUsers = _userManager.Users.Count();

            AdminCount = await CountUsersInRoleAsync("Administrator");
            DoctorCount = await CountUsersInRoleAsync("Doctor");
            AssistantCount = await CountUsersInRoleAsync("Assistant");
            PatientCount = await CountUsersInRoleAsync("Patient");

            var settings = await _db.SystemSettings.SingleOrDefaultAsync();
            if (settings != null)
            {
                ClinicName = string.IsNullOrWhiteSpace(settings.ClinicName)
                    ? "Smart Medical Platform"
                    : settings.ClinicName;

                MaxUploadMb = settings.MaxUploadMb;
            }
        }

        private async Task<int> CountUsersInRoleAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                return 0;

            var users = await _userManager.GetUsersInRoleAsync(roleName);
            return users.Count;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Administrator.Overview
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IndexModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public int TotalUsers { get; set; }
        public int TotalRoles { get; set; }
        public Dictionary<string, int> UsersPerRole { get; set; } = new();

        public async Task OnGetAsync()
        {
            TotalUsers = await _userManager.Users.CountAsync();
            TotalRoles = await _roleManager.Roles.CountAsync();

            var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                UsersPerRole[role] = usersInRole.Count;
            }
        }
    }
}

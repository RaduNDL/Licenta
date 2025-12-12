using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        public async Task OnGet()
        {
            TotalUsers = _userManager.Users.Count();
            TotalRoles = _roleManager.Roles.Count();

            foreach (var role in _roleManager.Roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
                UsersPerRole[role.Name!] = usersInRole.Count;
            }
        }
    }
}
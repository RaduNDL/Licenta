using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Areas.Admin.Pages.Staff
{
    [Authorize(Roles = "Administrator")]
    public class AssignmentsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AssignmentsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<DoctorAssignmentVm> DoctorAssignments { get; set; } = new();
        public SelectList AvailableAssistants { get; set; } = null!;

        public class DoctorAssignmentVm
        {
            public Guid DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;
            public string Specialty { get; set; } = string.Empty;
            public List<AssistantVm> AssignedAssistants { get; set; } = new();
        }

        public class AssistantVm
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAssignAsync(Guid doctorId, string assistantId)
        {
            if (doctorId == Guid.Empty || string.IsNullOrWhiteSpace(assistantId))
                return RedirectToPage();

            var doctor = await _db.Doctors
                .Include(d => d.Assistants)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor not found.";
                return RedirectToPage();
            }

            var assistant = await _userManager.FindByIdAsync(assistantId);
            if (assistant == null || assistant.IsSoftDeleted || !await _userManager.IsInRoleAsync(assistant, "Assistant"))
            {
                TempData["StatusMessage"] = "Assistant not found.";
                return RedirectToPage();
            }

            if (!doctor.Assistants.Any(a => a.Id == assistant.Id))
            {
                doctor.Assistants.Add(assistant);
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = "Assistant assigned successfully.";
            }
            else
            {
                TempData["StatusMessage"] = "Assistant is already assigned to this doctor.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(Guid doctorId, string assistantId)
        {
            if (doctorId == Guid.Empty || string.IsNullOrWhiteSpace(assistantId))
                return RedirectToPage();

            var doctor = await _db.Doctors
                .Include(d => d.Assistants)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor != null)
            {
                var assistant = doctor.Assistants.FirstOrDefault(a => a.Id == assistantId);
                if (assistant != null)
                {
                    doctor.Assistants.Remove(assistant);
                    await _db.SaveChangesAsync();
                    TempData["StatusMessage"] = "Assignment removed.";
                }
            }

            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            DoctorAssignments = await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Assistants)
                .Where(d => d.User != null && !d.User.IsSoftDeleted)
                .Select(d => new DoctorAssignmentVm
                {
                    DoctorId = d.Id,
                    DoctorName = DisplayName(d.User),
                    Specialty = string.IsNullOrWhiteSpace(d.Specialty) ? "General" : d.Specialty.Trim(),
                    AssignedAssistants = d.Assistants
                        .Where(a => !a.IsSoftDeleted)
                        .Select(a => new AssistantVm
                        {
                            Id = a.Id,
                            Name = DisplayName(a)
                        })
                        .ToList()
                })
                .ToListAsync();

            var assistantRoleId = await _db.Roles
                .Where(r => r.Name == "Assistant")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var allAssistants = await _db.Users
                .AsNoTracking()
                .Where(u =>
                    !u.IsSoftDeleted &&
                    _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == assistantRoleId))
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.Email)
                .Select(u => new
                {
                    Id = u.Id,
                    Name = DisplayName(u)
                })
                .ToListAsync();

            AvailableAssistants = new SelectList(allAssistants, "Id", "Name");
        }

        private static string DisplayName(ApplicationUser? user)
        {
            if (user == null) return "Unknown";
            var fullName = (user.FullName ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(fullName) &&
                !fullName.Equals("Default Assistant", StringComparison.OrdinalIgnoreCase) &&
                !fullName.Equals("Default Doctor", StringComparison.OrdinalIgnoreCase) &&
                !fullName.Equals("Default Patient", StringComparison.OrdinalIgnoreCase))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(user.UserName)) return user.UserName.Trim();
            if (!string.IsNullOrWhiteSpace(user.Email)) return user.Email.Trim();

            return "Unknown";
        }
    }
}
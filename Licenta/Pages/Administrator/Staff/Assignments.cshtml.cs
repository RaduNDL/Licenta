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
            if (doctorId == Guid.Empty || string.IsNullOrEmpty(assistantId)) return RedirectToPage();

            var assistant = await _userManager.FindByIdAsync(assistantId);
            if (assistant != null)
            {
                assistant.AssignedDoctorId = doctorId;
                await _userManager.UpdateAsync(assistant);
                TempData["StatusMessage"] = "Assistant assigned successfully.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(string assistantId)
        {
            var assistant = await _userManager.FindByIdAsync(assistantId);
            if (assistant != null)
            {
                assistant.AssignedDoctorId = null;
                await _userManager.UpdateAsync(assistant);
                TempData["StatusMessage"] = "Assignment removed.";
            }

            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            DoctorAssignments = await _db.Doctors
                .Include(d => d.User)
                .Include(d => d.Assistants)
                .Where(d => d.User != null && !d.User.IsSoftDeleted)
                .Select(d => new DoctorAssignmentVm
                {
                    DoctorId = d.Id,
                    DoctorName = d.User.FullName ?? d.User.Email ?? "Unknown",
                    Specialty = d.Specialty ?? "General",
                    AssignedAssistants = d.Assistants
                        .Where(a => !a.IsSoftDeleted)
                        .Select(a => new AssistantVm
                        {
                            Id = a.Id,
                            Name = a.FullName ?? a.Email ?? "Unknown"
                        }).ToList()
                }).ToListAsync();

            var allAssistants = await _userManager.GetUsersInRoleAsync("Assistant");

            var unassignedAssistants = allAssistants
                .Where(u => !u.IsSoftDeleted && u.AssignedDoctorId == null)
                .Select(u => new
                {
                    Id = u.Id,
                    Name = u.FullName ?? u.Email ?? "Unknown"
                })
                .ToList();

            AvailableAssistants = new SelectList(unassignedAssistants, "Id", "Name");
        }
    }
}
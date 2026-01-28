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

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class RescheduleRequestsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public RescheduleRequestsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<RowVm> Items { get; set; } = new();

        public class RowVm
        {
            public int Id { get; set; }
            public string Created { get; set; } = "";
            public string Patient { get; set; } = "";
            public string Doctor { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return;

            var clinicId = assistant.ClinicId;

            var list = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Where(r =>
                    string.IsNullOrWhiteSpace(clinicId) ||
                    r.Patient!.User!.ClinicId == clinicId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(200)
                .ToListAsync();

            Items = list.Select(r => new RowVm
            {
                Id = r.Id,
                Created = r.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Patient = r.Patient?.User?.FullName ?? r.Patient?.User?.Email ?? "Patient",
                Doctor = r.Doctor?.User?.FullName ?? r.Doctor?.User?.Email ?? "Doctor",
                Status = r.Status.ToString()
            }).ToList();
        }
    }
}

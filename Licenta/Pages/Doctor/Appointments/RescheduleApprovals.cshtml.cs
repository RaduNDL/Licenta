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

namespace Licenta.Pages.Doctor.Appointments
{
    [Authorize(Roles = "Doctor")]
    public class RescheduleApprovalsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public RescheduleApprovalsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
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
            public string OldTime { get; set; } = "";
            public string NewTime { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var doctor = await _db.Set<Licenta.Models.DoctorProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return;

            var list = await _db.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.SelectedOption)
                .Where(r =>
                    r.DoctorId == doctor.Id &&
                    r.Status == AppointmentRescheduleStatus.PatientSelected &&
                    r.SelectedOptionId != null)
                .OrderByDescending(r => r.UpdatedAtUtc)
                .Take(200)
                .ToListAsync();

            Items = list.Select(r => new RowVm
            {
                Id = r.Id,
                Created = r.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Patient = r.Patient?.User?.FullName ?? r.Patient?.User?.Email ?? "Patient",
                OldTime = r.OldScheduledAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                NewTime = r.SelectedOption == null ? "-" : r.SelectedOption.ProposedStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            }).ToList();
        }
    }
}

using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class TodayModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TodayModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public class ItemVm
        {
            public int Id { get; set; }
            public string TimeLocal { get; set; } = string.Empty;
            public string PatientName { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public bool CanCancel { get; set; }
        }

        public List<ItemVm> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);

            var appointments = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.ScheduledAt >= todayUtc && a.ScheduledAt < tomorrowUtc)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync();

            var nowUtc = DateTime.UtcNow;

            Items = appointments.Select(a =>
            {
                var patientName = a.Patient?.User != null
                    ? (a.Patient.User.FullName ?? a.Patient.User.Email ?? a.Patient.User.UserName ?? "Unknown patient")
                    : "Unknown patient";

                var doctorName = a.Doctor?.User != null
                    ? (a.Doctor.User.FullName ?? a.Doctor.User.Email ?? a.Doctor.User.UserName ?? "Unknown doctor")
                    : "Unknown doctor";

                var canCancel =
                    a.Status != AppointmentStatus.Cancelled &&
                    a.ScheduledAt >= nowUtc;

                return new ItemVm
                {
                    Id = a.Id,
                    TimeLocal = a.ScheduledAt.ToLocalTime().ToString("t"),
                    PatientName = patientName,
                    DoctorName = doctorName,
                    Reason = a.Reason ?? string.Empty,
                    Status = a.Status.ToString(),
                    CanCancel = canCancel
                };
            }).ToList();
        }
    }
}

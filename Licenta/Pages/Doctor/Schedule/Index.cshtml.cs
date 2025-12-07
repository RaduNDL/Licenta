using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Schedule
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

        public string? DoctorName { get; set; }

        [BindProperty]
        public List<DayAvailabilityInput> Days { get; set; } = new();

        public class DayAvailabilityInput
        {
            public DayOfWeek DayOfWeek { get; set; }

            public string DayName { get; set; } = "";

            public bool IsActive { get; set; }

            [DataType(DataType.Time)]
            public TimeSpan? StartTime { get; set; }

            [DataType(DataType.Time)]
            public TimeSpan? EndTime { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                doctor = new DoctorProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id
                   
                };
                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            DoctorName = doctor.User.FullName ?? doctor.User.Email ?? "Doctor";

            var existing = await _db.DoctorAvailabilities
                .Where(a => a.DoctorId == doctor.Id)
                .ToListAsync();

            Days = Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .OrderBy(d => d)
                .Select(d =>
                {
                    var avail = existing.FirstOrDefault(a => a.DayOfWeek == d);

                    return new DayAvailabilityInput
                    {
                        DayOfWeek = d,
                        DayName = d.ToString(),
                        IsActive = avail?.IsActive ?? false,
                        StartTime = avail?.StartTime,
                        EndTime = avail?.EndTime
                    };
                })
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

           
            if (doctor == null)
            {
                doctor = new DoctorProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id
                };
                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            if (!ModelState.IsValid)
            {
                foreach (var d in Days)
                {
                    d.DayName = d.DayOfWeek.ToString();
                }
                return Page();
            }

            var existing = await _db.DoctorAvailabilities
                .Where(a => a.DoctorId == doctor.Id)
                .ToListAsync();

            foreach (var day in Days)
            {
                var entity = existing.FirstOrDefault(a => a.DayOfWeek == day.DayOfWeek);

                if (!day.IsActive)
                {
                    if (entity != null)
                    {
                        _db.DoctorAvailabilities.Remove(entity);
                    }
                    continue;
                }

                if (day.StartTime == null || day.EndTime == null)
                {
                    ModelState.AddModelError("", $"Please set start/end time for {day.DayOfWeek} or uncheck it.");
                    break;
                }

                if (day.EndTime <= day.StartTime)
                {
                    ModelState.AddModelError("", $"End time must be after start time for {day.DayOfWeek}.");
                    break;
                }

                if (entity == null)
                {
                    entity = new DoctorAvailability
                    {
                        DoctorId = doctor.Id,
                        DayOfWeek = day.DayOfWeek,
                    };
                    _db.DoctorAvailabilities.Add(entity);
                }

                entity.IsActive = true;
                entity.StartTime = day.StartTime.Value;
                entity.EndTime = day.EndTime.Value;
            }

            if (!ModelState.IsValid)
            {
                foreach (var d in Days)
                {
                    d.DayName = d.DayOfWeek.ToString();
                }
                return Page();
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Schedule updated successfully.";
            return RedirectToPage();
        }
    }
}

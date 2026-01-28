using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        [BindProperty]
        public string? ScheduleText { get; set; }

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

        private static readonly Dictionary<string, DayOfWeek> DayMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["monday"] = DayOfWeek.Monday,
                ["tuesday"] = DayOfWeek.Tuesday,
                ["wednesday"] = DayOfWeek.Wednesday,
                ["thursday"] = DayOfWeek.Thursday,
                ["friday"] = DayOfWeek.Friday,
                ["saturday"] = DayOfWeek.Saturday,
                ["sunday"] = DayOfWeek.Sunday
            };

        private static readonly Dictionary<DayOfWeek, string> DayDisplayNames =
            new()
            {
                [DayOfWeek.Monday] = "Monday",
                [DayOfWeek.Tuesday] = "Tuesday",
                [DayOfWeek.Wednesday] = "Wednesday",
                [DayOfWeek.Thursday] = "Thursday",
                [DayOfWeek.Friday] = "Friday",
                [DayOfWeek.Saturday] = "Saturday",
                [DayOfWeek.Sunday] = "Sunday"
            };

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
                var created = new Licenta.Models.DoctorProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id
                };

                _db.Doctors.Add(created);
                await _db.SaveChangesAsync();

                doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == created.Id);
                if (doctor == null)
                    return Page();
            }

            DoctorName = doctor.User?.FullName ?? doctor.User?.Email ?? "Doctor";

            await LoadDaysFromDatabaseAsync(doctor.Id);

            var lines = new List<string>();
            foreach (var d in Days.Where(x => x.IsActive && x.StartTime.HasValue && x.EndTime.HasValue))
            {
                var name = d.DayName;
                var startStr = DateTime.Today.Add(d.StartTime.Value).ToString("HH:mm");
                var endStr = DateTime.Today.Add(d.EndTime.Value).ToString("HH:mm");
                lines.Add($"{name} {startStr} - {endStr}");
            }

            ScheduleText = string.Join(Environment.NewLine, lines);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                doctor = new Licenta.Models.DoctorProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id
                };

                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            if (string.IsNullOrWhiteSpace(ScheduleText))
            {
                ModelState.AddModelError(nameof(ScheduleText), "Please enter schedule text.");
                await LoadDaysFromDatabaseAsync(doctor.Id);
                return Page();
            }

            if (!TryParseScheduleText(ScheduleText, out var parsedDays, out var error))
            {
                ModelState.AddModelError(nameof(ScheduleText), error ?? "Could not parse schedule text.");
                await LoadDaysFromDatabaseAsync(doctor.Id);
                return Page();
            }

            Days = parsedDays;

            var existing = await _db.DoctorAvailabilities
                .Where(a => a.DoctorId == doctor.Id)
                .ToListAsync();

            foreach (var day in Days)
            {
                var entity = existing.FirstOrDefault(a => a.DayOfWeek == day.DayOfWeek);

                if (!day.IsActive)
                {
                    if (entity != null)
                        _db.DoctorAvailabilities.Remove(entity);
                    continue;
                }

                if (day.StartTime == null || day.EndTime == null)
                {
                    ModelState.AddModelError(nameof(ScheduleText), $"Please provide valid start and end time for {day.DayName}.");
                    await LoadDaysFromDatabaseAsync(doctor.Id);
                    return Page();
                }

                if (day.EndTime <= day.StartTime)
                {
                    ModelState.AddModelError(nameof(ScheduleText), $"End time must be after start time for {day.DayName}.");
                    await LoadDaysFromDatabaseAsync(doctor.Id);
                    return Page();
                }

                if (entity == null)
                {
                    entity = new DoctorAvailability
                    {
                        DoctorId = doctor.Id,
                        DayOfWeek = day.DayOfWeek
                    };
                    _db.DoctorAvailabilities.Add(entity);
                }

                entity.IsActive = true;
                entity.StartTime = day.StartTime.Value;
                entity.EndTime = day.EndTime.Value;
            }

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Schedule updated successfully.";
            return RedirectToPage();
        }

        private async Task LoadDaysFromDatabaseAsync(Guid doctorId)
        {
            var existing = await _db.DoctorAvailabilities
                .Where(a => a.DoctorId == doctorId)
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
                        DayName = DayDisplayNames.ContainsKey(d) ? DayDisplayNames[d] : d.ToString(),
                        IsActive = avail?.IsActive ?? false,
                        StartTime = avail?.StartTime,
                        EndTime = avail?.EndTime
                    };
                })
                .ToList();
        }

        private bool TryParseScheduleText(string text, out List<DayAvailabilityInput> parsedDays, out string? errorMessage)
        {
            parsedDays = Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .OrderBy(d => d)
                .Select(d => new DayAvailabilityInput
                {
                    DayOfWeek = d,
                    DayName = DayDisplayNames.ContainsKey(d) ? DayDisplayNames[d] : d.ToString(),
                    IsActive = false
                })
                .ToList();

            var byDay = parsedDays.ToDictionary(d => d.DayOfWeek);
            errorMessage = null;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                errorMessage = "Schedule text is empty.";
                return false;
            }

            var timeRegex = new Regex(@"(\d{1,2})(?::(\d{2}))?\s*(AM|PM|am|pm)?", RegexOptions.Compiled);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var firstSpace = line.IndexOf(' ');
                if (firstSpace <= 0)
                {
                    errorMessage = $"Cannot detect day name in line: '{line}'. Expected something like 'Monday 08:00 - 16:00'.";
                    return false;
                }

                var dayPart = line[..firstSpace].Trim();
                var rest = line[firstSpace..].Trim();

                if (!DayMap.TryGetValue(dayPart.ToLowerInvariant(), out var dayOfWeek))
                {
                    errorMessage = $"Unknown day name '{dayPart}' in line: '{line}'.";
                    return false;
                }

                var matches = timeRegex.Matches(rest);
                if (matches.Count < 2)
                {
                    errorMessage = $"Could not find both start and end time in line: '{line}'.";
                    return false;
                }

                if (!TryParseTimeMatch(matches[0], out var start))
                {
                    errorMessage = $"Invalid start time in line: '{line}'.";
                    return false;
                }

                if (!TryParseTimeMatch(matches[1], out var end))
                {
                    errorMessage = $"Invalid end time in line: '{line}'.";
                    return false;
                }

                if (end <= start)
                {
                    errorMessage = $"End time must be after start time in line: '{line}'.";
                    return false;
                }

                var dayInput = byDay[dayOfWeek];
                dayInput.IsActive = true;
                dayInput.StartTime = start;
                dayInput.EndTime = end;
            }

            return true;
        }

        private bool TryParseTimeMatch(Match m, out TimeSpan result)
        {
            result = TimeSpan.Zero;

            if (!m.Success)
                return false;

            if (!int.TryParse(m.Groups[1].Value, out var hour))
                return false;

            var minute = 0;
            if (m.Groups[2].Success && !int.TryParse(m.Groups[2].Value, out minute))
                return false;

            var ampm = m.Groups[3].Value?.ToLowerInvariant();

            if (!string.IsNullOrEmpty(ampm))
            {
                if (hour < 1 || hour > 12)
                    return false;

                if (ampm == "pm" && hour != 12)
                    hour += 12;
                if (ampm == "am" && hour == 12)
                    hour = 0;
            }

            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                return false;

            result = new TimeSpan(hour, minute, 0);
            return true;
        }
    }
}

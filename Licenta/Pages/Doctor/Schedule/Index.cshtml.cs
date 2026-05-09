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
        public string? ScheduleText { get; set; }

        public List<AvailabilityInterval> SavedIntervals { get; set; } = new();

        public class AvailabilityInterval
        {
            public DayOfWeek DayOfWeek { get; set; }
            public string DayName { get; set; } = "";
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
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
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                doctor = new Licenta.Models.DoctorProfile { Id = Guid.NewGuid(), UserId = user.Id };
                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            DoctorName = doctor.User?.FullName ?? doctor.User?.Email ?? "Doctor";
            await LoadScheduleAsync(doctor.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return RedirectToPage();

            if (string.IsNullOrWhiteSpace(ScheduleText))
            {
                return await OnPostClearAsync();
            }

            if (!TryParseScheduleText(ScheduleText, out var newIntervals, out var error))
            {
                ModelState.AddModelError(nameof(ScheduleText), error ?? "Invalid format.");
                await LoadScheduleAsync(doctor.Id);
                return Page();
            }

            var overlapError = FindOverlap(newIntervals);
            if (overlapError != null)
            {
                ModelState.AddModelError(nameof(ScheduleText), overlapError);
                await LoadScheduleAsync(doctor.Id);
                return Page();
            }

            var existing = await _db.DoctorAvailabilities.Where(a => a.DoctorId == doctor.Id).ToListAsync();
            _db.DoctorAvailabilities.RemoveRange(existing);

            foreach (var interval in newIntervals)
            {
                _db.DoctorAvailabilities.Add(new DoctorAvailability
                {
                    DoctorId = doctor.Id,
                    DayOfWeek = interval.DayOfWeek,
                    StartTime = interval.StartTime,
                    EndTime = interval.EndTime,
                    IsActive = true
                });
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Schedule updated with multiple slots support.";
            return RedirectToPage();
        }
        private static string? FindOverlap(List<AvailabilityInterval> intervals)
        {
            var byDay = intervals.GroupBy(i => i.DayOfWeek);
            foreach (var group in byDay)
            {
                var sorted = group.OrderBy(i => i.StartTime).ToList();
                for (int i = 1; i < sorted.Count; i++)
                {
                    if (sorted[i].StartTime < sorted[i - 1].EndTime)
                    {
                        return $"Overlapping intervals on {sorted[i].DayName}: " +
                               $"{sorted[i - 1].StartTime:hh\\:mm}-{sorted[i - 1].EndTime:hh\\:mm} " +
                               $"and {sorted[i].StartTime:hh\\:mm}-{sorted[i].EndTime:hh\\:mm}.";
                    }
                }
            }
            return null;
        }
        public async Task<IActionResult> OnPostClearAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor != null)
            {
                var existing = await _db.DoctorAvailabilities.Where(a => a.DoctorId == doctor.Id).ToListAsync();
                _db.DoctorAvailabilities.RemoveRange(existing);
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Schedule cleared successfully.";
            return RedirectToPage();
        }

        private async Task LoadScheduleAsync(Guid doctorId)
        {
            var items = await _db.DoctorAvailabilities
                .Where(a => a.DoctorId == doctorId)
                .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
                .ToListAsync();

            SavedIntervals = items.Select(a => new AvailabilityInterval
            {
                DayOfWeek = a.DayOfWeek,
                DayName = DayDisplayNames[a.DayOfWeek],
                StartTime = a.StartTime,
                EndTime = a.EndTime
            }).ToList();

            var lines = SavedIntervals.Select(i =>
                $"{i.DayName} {DateTime.Today.Add(i.StartTime):HH:mm} - {DateTime.Today.Add(i.EndTime):HH:mm}");

            ScheduleText = string.Join(Environment.NewLine, lines);
        }

        private bool TryParseScheduleText(string text, out List<AvailabilityInterval> intervals, out string? error)
        {
            intervals = new List<AvailabilityInterval>();
            error = null;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var timeRegex = new Regex(@"(\d{1,2})(?::(\d{2}))?\s*(AM|PM|am|pm)?", RegexOptions.Compiled);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                var firstSpace = line.IndexOf(' ');
                if (firstSpace <= 0) { error = $"Invalid line: {line}"; return false; }

                var dayPart = line[..firstSpace].Trim();
                if (!DayMap.TryGetValue(dayPart, out var dayOfWeek)) { error = $"Unknown day: {dayPart}"; return false; }

                var matches = timeRegex.Matches(line[firstSpace..]);
                if (matches.Count < 2) { error = $"Missing times in: {line}"; return false; }

                if (!TryParseTime(matches[0], out var start) || !TryParseTime(matches[1], out var end)) { error = $"Invalid time in: {line}"; return false; }
                if (end <= start) { error = $"End time must be after start: {line}"; return false; }

                intervals.Add(new AvailabilityInterval { DayOfWeek = dayOfWeek, DayName = DayDisplayNames[dayOfWeek], StartTime = start, EndTime = end });
            }
            return true;
        }

        private bool TryParseTime(Match m, out TimeSpan res)
        {
            res = TimeSpan.Zero;
            if (!int.TryParse(m.Groups[1].Value, out var h)) return false;
            var min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            var ampm = m.Groups[3].Value?.ToLower();
            if (!string.IsNullOrEmpty(ampm))
            {
                if (h == 12) h = 0;
                if (ampm == "pm") h += 12;
            }
            if (h < 0 || h > 23 || min < 0 || min > 59) return false;
            res = new TimeSpan(h, min, 0);
            return true;
        }
    }
}
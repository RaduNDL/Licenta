using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Reviews
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

        public IList<Review> MyReviews { get; set; } = new List<Review>();

        public string DoctorName { get; set; } = "";
        public string? Specialty { get; set; }

        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int ReviewsThisMonth { get; set; }
        public int FiveStarCount { get; set; }
        public int PositiveCount { get; set; }

        public Dictionary<int, int> RatingDistribution { get; set; } = new();
        public Dictionary<int, int> RatingPercentages { get; set; } = new();

        // Maps AuthorUserId -> profile photo URL (or null if none)
        public Dictionary<string, string?> AuthorAvatars { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                return NotFound("Doctor profile not found.");
            }

            DoctorName = doctor.User?.FullName ?? "Doctor";
            Specialty = doctor.Specialty;

            MyReviews = await _db.Reviews
                .Where(r => !r.IsDeleted
                         && r.Target == ReviewTarget.Doctor
                         && r.DoctorId == doctor.Id)
                .Include(r => r.Author)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToListAsync();

            TotalReviews = MyReviews.Count;
            AverageRating = TotalReviews == 0 ? 0 : Math.Round(MyReviews.Average(r => r.Rating), 1);

            var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            ReviewsThisMonth = MyReviews.Count(r => r.CreatedAtUtc >= firstOfMonth);

            FiveStarCount = MyReviews.Count(r => r.Rating == 5);
            PositiveCount = MyReviews.Count(r => r.Rating >= 4);

            for (int i = 1; i <= 5; i++)
            {
                var count = MyReviews.Count(r => r.Rating == i);
                RatingDistribution[i] = count;
                RatingPercentages[i] = TotalReviews == 0
                    ? 0
                    : (int)Math.Round((double)count / TotalReviews * 100);
            }

            await LoadAuthorAvatarsAsync();

            return Page();
        }

        private async Task LoadAuthorAvatarsAsync()
        {
            AuthorAvatars = new Dictionary<string, string?>();
            if (MyReviews.Count == 0) return;

            var authorUserIds = MyReviews.Select(r => r.AuthorUserId).Distinct().ToList();

            // Map UserId -> PatientId (same chain used by the navbar)
            var patientLookup = await _db.Patients.AsNoTracking()
                .Where(p => authorUserIds.Contains(p.UserId))
                .Select(p => new { p.Id, p.UserId })
                .ToListAsync();

            if (patientLookup.Count == 0) return;

            var patientIdToUserId = patientLookup.ToDictionary(p => p.Id, p => p.UserId);
            var patientIds = patientLookup.Select(p => p.Id).ToList();

            // Pull the latest ProfilePhoto per patient
            var photoRows = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => patientIds.Contains(a.PatientId) && a.Type == "ProfilePhoto")
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new { a.PatientId, a.FilePath })
                .ToListAsync();

            foreach (var row in photoRows)
            {
                if (patientIdToUserId.TryGetValue(row.PatientId, out var userId)
                    && !AuthorAvatars.ContainsKey(userId))
                {
                    AuthorAvatars[userId] = row.FilePath;
                }
            }
        }

        public static string GetInitial(string? name)
            => string.IsNullOrWhiteSpace(name) ? "P" : name.Trim().Substring(0, 1).ToUpper();
    }
}
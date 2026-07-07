using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Reviews
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IList<Review> DoctorReviews { get; set; } = new List<Review>();
        public IList<Review> MyReviews { get; set; } = new List<Review>();
        public IList<DoctorLite> AvailableDoctors { get; set; } = new List<DoctorLite>();
        public Dictionary<string, string?> AuthorAvatars { get; set; } = new();

        public int TotalDoctorReviews { get; set; }
        public int MyReviewsCount { get; set; }

        [TempData] public string? StatusMessage { get; set; }
        [TempData] public string? StatusType { get; set; }

        [BindProperty]
        public ReviewInput Input { get; set; } = new();

        public class ReviewInput
        {
            public Guid? Id { get; set; }

            [Required(ErrorMessage = "Please select a doctor.")]
            public Guid? DoctorId { get; set; }

            [Range(1, 5, ErrorMessage = "Please choose a rating between 1 and 5 stars.")]
            public int Rating { get; set; } = 5;

            [StringLength(120)]
            public string? Title { get; set; }

            [Required(ErrorMessage = "Please write a short comment.")]
            [StringLength(2000, MinimumLength = 3, ErrorMessage = "Comment must be between 3 and 2000 characters.")]
            public string Comment { get; set; } = string.Empty;
        }

        public class DoctorLite
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string? Specialty { get; set; }
            public string? ImagePath { get; set; }
            public double Average { get; set; }
            public int Count { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            NormalizeInput();

            if (!ModelState.IsValid)
            {
                await LoadDataAsync();
                return Page();
            }

            if (!Input.DoctorId.HasValue || !await DoctorIsReviewableAsync(Input.DoctorId.Value, user))
            {
                ModelState.AddModelError(nameof(Input) + "." + nameof(Input.DoctorId), "Please select an available doctor.");
                await LoadDataAsync();
                return Page();
            }

            var alreadyDoctorReview = await _db.Reviews
                .AnyAsync(r => r.AuthorUserId == user.Id
                            && r.Target == ReviewTarget.Doctor
                            && r.DoctorId == Input.DoctorId
                            && !r.IsDeleted);

            if (alreadyDoctorReview)
            {
                StatusMessage = "You have already reviewed this doctor. You can edit your existing review instead.";
                StatusType = "warning";
                return RedirectToPage();
            }

            var review = new Review
            {
                Id = Guid.NewGuid(),
                AuthorUserId = user.Id,
                Target = ReviewTarget.Doctor, // Fortam target-ul catre Doctor
                DoctorId = Input.DoctorId,
                Rating = Input.Rating,
                Title = string.IsNullOrWhiteSpace(Input.Title) ? null : Input.Title.Trim(),
                Comment = Input.Comment.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            StatusMessage = "Thank you! Your review has been posted.";
            StatusType = "success";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (Input.Id == null)
            {
                StatusMessage = "Invalid review.";
                StatusType = "danger";
                return RedirectToPage();
            }

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.Id == Input.Id && r.AuthorUserId == user.Id);

            if (review == null)
            {
                StatusMessage = "Review not found or you are not allowed to edit it.";
                StatusType = "danger";
                return RedirectToPage();
            }

            Input.DoctorId = review.DoctorId;

            if (Input.Rating < 1 || Input.Rating > 5)
                ModelState.AddModelError(nameof(Input) + "." + nameof(Input.Rating), "Please choose a rating between 1 and 5 stars.");

            if (string.IsNullOrWhiteSpace(Input.Comment) || Input.Comment.Trim().Length < 3)
                ModelState.AddModelError(nameof(Input) + "." + nameof(Input.Comment), "Comment must be between 3 and 2000 characters.");

            if (!ModelState.IsValid)
            {
                await LoadDataAsync();
                return Page();
            }

            review.Rating = Input.Rating;
            review.Title = string.IsNullOrWhiteSpace(Input.Title) ? null : Input.Title.Trim();
            review.Comment = Input.Comment.Trim();
            review.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            StatusMessage = "Your review has been updated.";
            StatusType = "success";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.AuthorUserId == user.Id);

            if (review == null)
            {
                StatusMessage = "Review not found or you are not allowed to delete it.";
                StatusType = "danger";
                return RedirectToPage();
            }

            review.IsDeleted = true;
            review.DeletedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            StatusMessage = "Your review has been deleted.";
            StatusType = "success";
            return RedirectToPage();
        }

        private void NormalizeInput()
        {
            if (Input.Rating < 1 || Input.Rating > 5)
            {
                ModelState.AddModelError(nameof(Input) + "." + nameof(Input.Rating), "Please choose a rating between 1 and 5 stars.");
            }

            if (Input.Title != null) Input.Title = Input.Title.Trim();
            if (Input.Comment != null) Input.Comment = Input.Comment.Trim();
        }

        private async Task LoadDataAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            // Extragem doar recenziile pentru doctori
            var all = await _db.Reviews
                .Where(r => !r.IsDeleted && r.Target == ReviewTarget.Doctor)
                .Include(r => r.Author)
                .Include(r => r.Doctor).ThenInclude(d => d!.User)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToListAsync();

            DoctorReviews = all.ToList();
            MyReviews = all.Where(r => r.AuthorUserId == user.Id).ToList();

            TotalDoctorReviews = DoctorReviews.Count;
            MyReviewsCount = MyReviews.Count;

            var clinicId = (user.ClinicId ?? "").Trim();

            var docsQuery = _db.Doctors
                .Include(d => d.User)
                .Where(d => d.User != null && !d.User.IsSoftDeleted);

            if (!string.IsNullOrWhiteSpace(clinicId))
                docsQuery = docsQuery.Where(d => d.User!.ClinicId == clinicId);

            var docs = await docsQuery
                .OrderBy(d => d.User!.FullName)
                .ToListAsync();

            var doctorStats = DoctorReviews
                .Where(r => r.DoctorId.HasValue)
                .GroupBy(r => r.DoctorId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => new { Avg = g.Average(x => x.Rating), Cnt = g.Count() });

            AvailableDoctors = docs.Select(d => new DoctorLite
            {
                Id = d.Id,
                Name = d.User?.FullName ?? "Unknown",
                Specialty = d.Specialty,
                ImagePath = d.ProfileImagePath,
                Average = doctorStats.TryGetValue(d.Id, out var s) ? Math.Round(s.Avg, 1) : 0,
                Count = doctorStats.TryGetValue(d.Id, out var s2) ? s2.Cnt : 0
            }).ToList();

            await LoadAuthorAvatarsAsync(all);
        }

        private async Task<bool> DoctorIsReviewableAsync(Guid doctorId, ApplicationUser user)
        {
            var clinicId = (user.ClinicId ?? "").Trim();

            return await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .AnyAsync(d =>
                    d.Id == doctorId &&
                    d.User != null &&
                    !d.User.IsSoftDeleted &&
                    (string.IsNullOrWhiteSpace(clinicId) || d.User.ClinicId == clinicId));
        }

        private async Task LoadAuthorAvatarsAsync(List<Review> reviews)
        {
            AuthorAvatars = new Dictionary<string, string?>();
            if (reviews.Count == 0) return;

            var authorUserIds = reviews.Select(r => r.AuthorUserId).Distinct().ToList();

            var patientLookup = await _db.Patients.AsNoTracking()
                .Where(p => authorUserIds.Contains(p.UserId))
                .Select(p => new { p.Id, p.UserId })
                .ToListAsync();

            if (patientLookup.Count == 0) return;

            var patientIdToUserId = patientLookup.ToDictionary(p => p.Id, p => p.UserId);
            var patientIds = patientLookup.Select(p => p.Id).ToList();

            var photoRows = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => patientIds.Contains(a.PatientId) && a.Type == "ProfilePhoto")
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new { a.Id, a.PatientId, a.FilePath })
                .ToListAsync();

            foreach (var row in photoRows)
            {
                if (!patientIdToUserId.TryGetValue(row.PatientId, out var userId))
                    continue;

                if (AuthorAvatars.ContainsKey(userId))
                    continue;

                var url = (row.FilePath ?? "").StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
                    ? row.FilePath
                    : $"/Files/Attachment?id={row.Id}";

                AuthorAvatars[userId] = url;
            }
        }

        public static string GetInitial(string? name)
            => string.IsNullOrWhiteSpace(name) ? "U" : name.Trim().Substring(0, 1).ToUpper();
    }
}
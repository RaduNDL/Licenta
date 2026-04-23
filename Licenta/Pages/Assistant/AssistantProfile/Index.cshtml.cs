using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssistantProfileEntity = Licenta.Models.AssistantProfile;

namespace Licenta.Pages.Assistant.AssistantProfile
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? PhotoFile { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = "Assistant";
        public string DisplaySubtitle { get; set; } = "Assistant";
        public string Email { get; set; } = string.Empty;
        public string ClinicId { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }

        public int StatsAppointments { get; set; }
        public int StatsPendingTasks { get; set; }
        public int StatsUnreadMessages { get; set; }
        public int StatsPatients { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(80, MinimumLength = 2)]
            public string FullName { get; set; } = string.Empty;

            [Phone]
            [StringLength(30)]
            public string? Phone { get; set; }

            [StringLength(80)]
            public string? Department { get; set; }

            [StringLength(2000)]
            public string? Bio { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var assistant = await EnsureAssistantProfileAsync(user.Id);

            UserId = user.Id;
            Email = user.Email ?? user.UserName ?? string.Empty;
            ClinicId = user.ClinicId ?? string.Empty;
            DisplayName = user.FullName ?? user.Email ?? "Assistant";

            Input = new InputModel
            {
                FullName = user.FullName ?? string.Empty,
                Phone = assistant.Phone,
                Department = assistant.Department,
                Bio = assistant.Bio
            };

            ProfileImageUrl = assistant.ProfileImagePath;
            DisplaySubtitle = string.IsNullOrWhiteSpace(Input.Department) ? "Assistant" : Input.Department;

            await LoadStatsAsync(user.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var assistant = await EnsureAssistantProfileAsync(user.Id);

            if (!ModelState.IsValid)
            {
                await HydrateHeaderAsync(user.Id, assistant.Id);
                return Page();
            }

            user.FullName = (Input.FullName ?? string.Empty).Trim();

            var updUser = await _userManager.UpdateAsync(user);
            if (!updUser.Succeeded)
            {
                foreach (var e in updUser.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                await HydrateHeaderAsync(user.Id, assistant.Id);
                return Page();
            }

            assistant.Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
            assistant.Department = string.IsNullOrWhiteSpace(Input.Department) ? null : Input.Department.Trim();
            assistant.Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Profile updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUploadPhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var assistant = await EnsureAssistantProfileAsync(user.Id);

            if (PhotoFile == null || PhotoFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a photo to upload.";
                return RedirectToPage();
            }

            if (PhotoFile.Length > 2 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Photo is too large. Max 2MB.";
                return RedirectToPage();
            }

            var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            var ext = Path.GetExtension(PhotoFile.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                TempData["ErrorMessage"] = "Invalid format. Use PNG/JPG/WEBP.";
                return RedirectToPage();
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "assistants");
            Directory.CreateDirectory(folder);

            var fileName = $"assistant_{assistant.Id:N}_{Guid.NewGuid():N}{ext}";
            var absPath = Path.Combine(folder, fileName);

            using (var fs = new FileStream(absPath, FileMode.Create))
                await PhotoFile.CopyToAsync(fs);

            if (!string.IsNullOrWhiteSpace(assistant.ProfileImagePath))
                TryDeleteOldPhoto(assistant.ProfileImagePath);

            assistant.ProfileImagePath = "/uploads/assistants/" + fileName;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Photo updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemovePhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var assistant = await EnsureAssistantProfileAsync(user.Id);

            if (!string.IsNullOrWhiteSpace(assistant.ProfileImagePath))
            {
                TryDeleteOldPhoto(assistant.ProfileImagePath);
                assistant.ProfileImagePath = null;
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Photo removed.";
            return RedirectToPage();
        }

        private async Task<AssistantProfileEntity> EnsureAssistantProfileAsync(string userId)
        {
            var assistant = await _db.Assistants.FirstOrDefaultAsync(p => p.UserId == userId);
            if (assistant != null)
                return assistant;

            var created = new AssistantProfileEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };

            _db.Assistants.Add(created);
            await _db.SaveChangesAsync();
            return created;
        }

        private async Task HydrateHeaderAsync(string userId, Guid assistantId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                UserId = user.Id;
                Email = user.Email ?? user.UserName ?? string.Empty;
                ClinicId = user.ClinicId ?? string.Empty;
                DisplayName = user.FullName ?? user.Email ?? "Assistant";
            }

            var assistant = await _db.Assistants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == assistantId);
            if (assistant != null)
            {
                Input ??= new InputModel();
                Input.Phone = assistant.Phone;
                Input.Department = assistant.Department;
                Input.Bio = assistant.Bio;

                DisplaySubtitle = string.IsNullOrWhiteSpace(Input.Department) ? "Assistant" : Input.Department;
                ProfileImageUrl = assistant.ProfileImagePath;

                await LoadStatsAsync(userId);
            }
        }

        private async Task LoadStatsAsync(string userId)
        {
            StatsAppointments = await _db.Appointments.AsNoTracking()
                .Where(a => a.Status != AppointmentStatus.Cancelled)
                .CountAsync();

            StatsPendingTasks = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => a.Status == AttachmentStatus.Pending)
                .CountAsync();

            StatsPatients = await _db.Patients.AsNoTracking()
                .Where(p => p.User != null && (p.User.ClinicId ?? string.Empty) == (ClinicId ?? string.Empty))
                .CountAsync();

            StatsUnreadMessages = await _db.InternalMessages.AsNoTracking()
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .CountAsync();
        }

        private void TryDeleteOldPhoto(string webPath)
        {
            try
            {
                if (!webPath.StartsWith("/uploads/assistants/", StringComparison.OrdinalIgnoreCase))
                    return;

                var rel = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var abs = Path.Combine(_env.WebRootPath, rel);
                if (System.IO.File.Exists(abs))
                    System.IO.File.Delete(abs);
            }
            catch
            {
            }
        }
    }
}
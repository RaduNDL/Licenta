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
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.AdminProfile
{
    [Authorize(Roles = "Administrator")]
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

        public string FullName { get; private set; } = "Administrator";
        public string Email { get; private set; } = "";
        public string UserName { get; private set; } = "";
        public string? ClinicName { get; private set; }
        public string? ProfileImageUrl { get; private set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? PhotoFile { get; set; }

        public sealed class InputModel
        {
            [Required]
            [StringLength(120)]
            [Display(Name = "Full name")]
            public string FullName { get; set; } = "";

            [StringLength(40)]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }
        }

        // ── OnGet ──────────────────────────────────────────────────────

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await LoadAsync(user);
            return Page();
        }

        // ── OnPost (save profile info) ─────────────────────────────────

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var changed = false;

            var newName = (Input.FullName ?? "").Trim();
            if (user.FullName != newName)
            {
                user.FullName = newName;
                changed = true;
            }

            var newPhone = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();
            if (user.PhoneNumber != newPhone)
            {
                user.PhoneNumber = newPhone;
                changed = true;
            }

            if (changed)
            {
                var res = await _userManager.UpdateAsync(user);
                if (!res.Succeeded)
                {
                    foreach (var e in res.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);

                    await LoadAsync(user);
                    return Page();
                }

                TempData["StatusMessage"] = "Profile updated successfully.";
            }
            else
            {
                TempData["StatusMessage"] = "No changes to save.";
            }

            return RedirectToPage();
        }

        // ── OnPostUploadPhoto ──────────────────────────────────────────

        public async Task<IActionResult> OnPostUploadPhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (PhotoFile == null || PhotoFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a photo to upload.";
                return RedirectToPage();
            }

            if (PhotoFile.Length > 2 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "The photo is too large. Maximum allowed size is 2 MB.";
                return RedirectToPage();
            }

            var ext = Path.GetExtension(PhotoFile.FileName).ToLowerInvariant();
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            if (!System.Array.Exists(allowed, e => e == ext))
            {
                TempData["ErrorMessage"] = "Invalid format. Use PNG, JPG, JPEG or WEBP.";
                return RedirectToPage();
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "admins");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"admin_{user.Id}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
                await PhotoFile.CopyToAsync(stream);

            // Sterge poza veche
            if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
                TryDeleteOldPhoto(user.ProfileImagePath);

            user.ProfileImagePath = "/uploads/admins/" + fileName;
            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Profile photo updated successfully.";
            return RedirectToPage();
        }

        // ── OnPostRemovePhoto ──────────────────────────────────────────

        public async Task<IActionResult> OnPostRemovePhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
            {
                TryDeleteOldPhoto(user.ProfileImagePath);
                user.ProfileImagePath = null;
                await _userManager.UpdateAsync(user);
            }

            TempData["StatusMessage"] = "Profile photo removed.";
            return RedirectToPage();
        }

        // ── Helpers ────────────────────────────────────────────────────

        private async Task LoadAsync(ApplicationUser user)
        {
            FullName = user.FullName ?? user.UserName ?? "Administrator";
            Email = user.Email ?? "";
            UserName = user.UserName ?? "";
            ProfileImageUrl = user.ProfileImagePath;

            Input = new InputModel
            {
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber
            };

            var settings = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ClinicName = settings?.ClinicName;
        }

        private void TryDeleteOldPhoto(string webPath)
        {
            try
            {
                if (!webPath.StartsWith("/uploads/admins/", StringComparison.OrdinalIgnoreCase))
                    return;

                var rel = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var abs = Path.Combine(_env.WebRootPath, rel);

                if (System.IO.File.Exists(abs))
                    System.IO.File.Delete(abs);
            }
            catch { }
        }
    }
}
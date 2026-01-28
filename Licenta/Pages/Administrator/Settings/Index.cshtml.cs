using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.Settings
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public IndexModel(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        public class InputModel
        {
            [MaxLength(200)]
            public string? ClinicName { get; set; }

            [Range(1, 200)]
            public int MaxUploadMb { get; set; } = 10;

            public string? LogoPath { get; set; }

            [Range(4, 128)]
            public int PasswordMinLength { get; set; } = 6;

            public bool RequireDigit { get; set; }

            public bool RequireUppercase { get; set; }

            public bool RequireSpecialChar { get; set; }
        }

        public async Task OnGetAsync()
        {
            var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            if (s == null)
            {
                Input = new InputModel();
                return;
            }

            Input = new InputModel
            {
                ClinicName = s.ClinicName,
                MaxUploadMb = s.MaxUploadMb,
                LogoPath = s.LogoPath,
                PasswordMinLength = s.PasswordMinLength,
                RequireDigit = s.RequireDigit,
                RequireUppercase = s.RequireUppercase,
                RequireSpecialChar = s.RequireSpecialChar
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await ReloadLogoPathAsync();
                return Page();
            }

            var s = await _db.SystemSettings.FirstOrDefaultAsync();
            if (s == null)
            {
                s = new SystemSetting();
                _db.SystemSettings.Add(s);
            }

            s.ClinicName = (Input.ClinicName ?? "").Trim();
            s.MaxUploadMb = Clamp(Input.MaxUploadMb, 1, 200);

            s.PasswordMinLength = Clamp(Input.PasswordMinLength, 4, 128);
            s.RequireDigit = Input.RequireDigit;
            s.RequireUppercase = Input.RequireUppercase;
            s.RequireSpecialChar = Input.RequireSpecialChar;

            if (LogoFile != null && LogoFile.Length > 0)
            {
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
                var ext = Path.GetExtension(LogoFile.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                {
                    TempData["Error"] = "Invalid logo format. Allowed: .png, .jpg, .jpeg, .webp, .svg";
                    await ReloadLogoPathAsync();
                    return Page();
                }

                var folder = Path.Combine(_env.WebRootPath, "uploads", "logos");
                Directory.CreateDirectory(folder);

                var fileName = $"logo_{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(folder, fileName);

                await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await LogoFile.CopyToAsync(fs);
                }

                s.LogoPath = "/uploads/logos/" + fileName;
            }

            await _db.SaveChangesAsync();

            TempData["Status"] = "Settings updated.";
            return RedirectToPage();
        }

        private async Task ReloadLogoPathAsync()
        {
            var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            if (s != null)
                Input.LogoPath = s.LogoPath;
        }

        private static int Clamp(int v, int min, int max)
            => v < min ? min : (v > max ? max : v);
    }
}

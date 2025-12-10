using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace Licenta.Pages.Administrator.Settings
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        // pentru upload logo
        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        public class InputModel
        {
            // BRANDING
            [Display(Name = "Clinic name")]
            public string? ClinicName { get; set; }

            [Display(Name = "Max upload (MB)")]
            public int MaxUploadMb { get; set; } = 10;

            public string? LogoPath { get; set; }

            // SMTP
            [Display(Name = "SMTP server")]
            public string? SmtpServer { get; set; }

            [Display(Name = "SMTP port")]
            public int SmtpPort { get; set; } = 587;

            [Display(Name = "Use SSL/TLS")]
            public bool SmtpUseSSL { get; set; }

            [Display(Name = "SMTP user")]
            public string? SmtpUser { get; set; }

            [Display(Name = "SMTP password")]
            [DataType(DataType.Password)]
            public string? SmtpPassword { get; set; }  // poate fi gol => păstrăm vechea parolă

            // SECURITY
            [Display(Name = "Password min length")]
            public int PasswordMinLength { get; set; } = 6;

            [Display(Name = "Require digit")]
            public bool RequireDigit { get; set; }

            [Display(Name = "Require uppercase")]
            public bool RequireUppercase { get; set; }

            [Display(Name = "Require special character")]
            public bool RequireSpecialChar { get; set; }
        }

        // ==================== GET ====================
        public async Task OnGetAsync()
        {
            var settings = await _db.SystemSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                Input = new InputModel();
                return;
            }

            Input = new InputModel
            {
                ClinicName = settings.ClinicName,
                MaxUploadMb = settings.MaxUploadMb,
                LogoPath = settings.LogoPath,

                SmtpServer = settings.SmtpServer,
                SmtpPort = settings.SmtpPort,
                SmtpUser = settings.SmtpUser,
                // parola nu o trimitem în view
                SmtpUseSSL = settings.SmtpUseSSL,

                PasswordMinLength = settings.PasswordMinLength,
                RequireDigit = settings.RequireDigit,
                RequireUppercase = settings.RequireUppercase,
                RequireSpecialChar = settings.RequireSpecialChar
            };
        }

        // ==================== POST ====================
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var settings = await _db.SystemSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new SystemSetting();
                _db.SystemSettings.Add(settings);
            }

            // BRANDING
            settings.ClinicName = Input.ClinicName?.Trim() ?? string.Empty;
            settings.MaxUploadMb = Input.MaxUploadMb;

            // upload logo (opțional)
            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploadsFolder = Path.Combine("wwwroot", "uploads", "logos");
                Directory.CreateDirectory(uploadsFolder);

                var ext = Path.GetExtension(LogoFile.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";

                var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(stream);
                }

                settings.LogoPath = "/uploads/logos/" + fileName;
            }

            // SMTP
            settings.SmtpServer = Input.SmtpServer?.Trim() ?? string.Empty;
            settings.SmtpPort = Input.SmtpPort;
            settings.SmtpUser = Input.SmtpUser?.Trim() ?? string.Empty;
            settings.SmtpUseSSL = Input.SmtpUseSSL;

            // dacă adminul completează o parolă nouă -> o suprascriem
            if (!string.IsNullOrWhiteSpace(Input.SmtpPassword))
            {
                settings.SmtpPassword = Input.SmtpPassword;
            }

            // siguranță: să nu fie niciodată NULL (coloană NOT NULL)
            if (settings.SmtpPassword == null)
                settings.SmtpPassword = string.Empty;

            // SECURITY
            settings.PasswordMinLength = Input.PasswordMinLength;
            settings.RequireDigit = Input.RequireDigit;
            settings.RequireUppercase = Input.RequireUppercase;
            settings.RequireSpecialChar = Input.RequireSpecialChar;

            await _db.SaveChangesAsync();

            TempData["Status"] = "Settings updated.";
            return RedirectToPage();
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
        public SystemSetting Input { get; set; }

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Input = await _db.SystemSettings.SingleAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var settings = await _db.SystemSettings.SingleAsync();

            // Upload logo
            if (LogoFile != null)
            {
                var fileName = "logo_" + Guid.NewGuid() + Path.GetExtension(LogoFile.FileName);
                var uploadFolder = Path.Combine(_env.WebRootPath, "uploads");
                var path = Path.Combine(uploadFolder, fileName);

                Directory.CreateDirectory(uploadFolder);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(stream);
                }

                settings.LogoPath = "/uploads/" + fileName;
            }

            // Map fields
            settings.ClinicName = Input.ClinicName;
            settings.MaxUploadMb = Input.MaxUploadMb;
            settings.SmtpServer = Input.SmtpServer;
            settings.SmtpPort = Input.SmtpPort;
            settings.SmtpUser = Input.SmtpUser;
            settings.SmtpPassword = Input.SmtpPassword;
            settings.SmtpUseSSL = Input.SmtpUseSSL;
            settings.PasswordMinLength = Input.PasswordMinLength;
            settings.RequireDigit = Input.RequireDigit;
            settings.RequireUppercase = Input.RequireUppercase;
            settings.RequireSpecialChar = Input.RequireSpecialChar;

            await _db.SaveChangesAsync();

            TempData["Status"] = "Settings updated successfully!";
            return RedirectToPage();
        }
    }
}

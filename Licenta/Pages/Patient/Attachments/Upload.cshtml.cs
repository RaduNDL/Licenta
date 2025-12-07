using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Attachments
{
    [Authorize(Roles = "Patient")]
    public class UploadModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public UploadModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public SelectList Doctors { get; set; } = default!;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public List<IFormFile> Files { get; set; } = new();

        public class InputModel
        {
            [Required]
            public Guid DoctorId { get; set; }

            [MaxLength(2000)]
            public string? Notes { get; set; }
        }

        public async Task OnGetAsync()
        {
            Doctors = new SelectList(
                await _db.Doctors
                    .Include(d => d.User)
                    .OrderBy(d => d.User.FullName)
                    .ToListAsync(),
                "Id", "User.FullName");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await OnGetAsync(); // reload doctors list in case of validation error

            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Please fix form errors.";
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return Page();
            }

            var patient = await _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
            {
                TempData["StatusMessage"] = "Patient profile not found.";
                return Page();
            }

            if (Files == null || Files.Count == 0)
            {
                TempData["StatusMessage"] = "Please select at least one image.";
                return Page();
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "patient", patient.Id.ToString());
            Directory.CreateDirectory(uploadsRoot);

            int saved = 0;
            foreach (var file in Files)
            {
                if (file.Length <= 0) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                    continue; // skip unsupported files

                if (file.Length > 10 * 1024 * 1024) // 10MB limit
                    continue;

                var safeName = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploadsRoot, safeName);

                using (var stream = System.IO.File.Create(fullPath))
                    await file.CopyToAsync(stream);

                var relPath = $"/uploads/patient/{patient.Id}/{safeName}";

                var attachment = new MedicalAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileName(file.FileName),
                    FilePath = relPath,
                    Type = "MedicalImage",
                    UploadedAt = DateTime.UtcNow,
                    Status = AttachmentStatus.Pending, 
                    ValidationNotes = Input.Notes,
                    DoctorId = Input.DoctorId,
                    PatientId = patient.Id,
                    UploadedByAssistantId = null
                };

                _db.MedicalAttachments.Add(attachment);
                saved++;
            }

            if (saved > 0)
            {
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = $"{saved} file(s) uploaded and sent for validation.";
                return RedirectToPage();
            }

            TempData["StatusMessage"] = "No files were uploaded.";
            return Page();
        }
    }
}

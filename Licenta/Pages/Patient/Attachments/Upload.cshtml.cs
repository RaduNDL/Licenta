using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;

namespace Licenta.Pages.Patient.Attachments
{
    [Authorize(Roles = "Patient")]
    public class UploadModel : PageModel
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".dcm", ".tif", ".tiff", ".pdf"
        };

        private const long MaxFileSizeBytes = 20L * 1024 * 1024; 

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public UploadModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
            _notifier = notifier;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList DoctorList { get; set; } = null!;

        public class InputModel
        {
            [Required(ErrorMessage = "Please select a doctor.")]
            public Guid DoctorId { get; set; }

            [Required(ErrorMessage = "Please select a document type.")]
            public string Type { get; set; } = "";

            public string? Notes { get; set; }

            [Required(ErrorMessage = "Please select an image file.")]
            public IFormFile File { get; set; } = null!;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDoctorsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDoctorsAsync();
                return Page();
            }

            if (Input.File.Length == 0)
            {
                ModelState.AddModelError("Input.File", "The file is empty.");
                await LoadDoctorsAsync();
                return Page();
            }

            if (Input.File.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError("Input.File",
                    $"File is too large. Max size is {MaxFileSizeBytes / (1024 * 1024)} MB.");
                await LoadDoctorsAsync();
                return Page();
            }

            var ext = Path.GetExtension(Input.File.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("Input.File",
                    "Unsupported file type. Allowed: JPG/JPEG, PNG, DCM, TIFF, PDF.");
                await LoadDoctorsAsync();
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null)
                return Forbid();

            var selectedDoctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == Input.DoctorId);

            if (selectedDoctor == null || selectedDoctor.User == null)
            {
                ModelState.AddModelError("Input.DoctorId", "Selected doctor is not valid.");
                await LoadDoctorsAsync();
                return Page();
            }

            var uploadsFolder = Path.Combine(
                _env.ContentRootPath,
                "Files", "uploads", "patient", patient.Id.ToString(), "cbis_ddsm");

            Directory.CreateDirectory(uploadsFolder);

            var originalName = Path.GetFileName(Input.File.FileName);
            var sanitized = SanitizeFileName(originalName, fallbackExt: ext);
            var uniqueFileName = $"{Guid.NewGuid():N}_{sanitized}";
            var absolutePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using (var stream = new FileStream(absolutePath, FileMode.Create))
            {
                await Input.File.CopyToAsync(stream);
            }

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = Input.DoctorId,
                FileName = originalName,
                FilePath = absolutePath,
                ContentType = Input.File.ContentType,
                Type = string.IsNullOrWhiteSpace(Input.Type) ? "CBIS-DDSM Breast Image" : Input.Type.Trim(),
                PatientNotes = Input.Notes,
                Status = AttachmentStatus.Pending,
                UploadedAt = DateTime.UtcNow
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            await _notifier.NotifyAsync(
                selectedDoctor.User,
                NotificationType.Document,
                "New breast image uploaded",
                $"Patient {user.FullName ?? user.Email} uploaded a new image: {attachment.FileName}",
                actionUrl: "/Doctor/Attachments/Inbox",
                actionText: "Review Image",
                relatedEntity: "MedicalAttachment",
                relatedEntityId: attachment.Id.ToString(),
                sendEmail: false);

            TempData["StatusMessage"] = $"Image uploaded successfully. Dr. {selectedDoctor.User.FullName} has been notified.";
            return RedirectToPage("/Patient/Attachments/Index");
        }

        private async Task LoadDoctorsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var clinicId = (user?.ClinicId ?? "").Trim();

            var query = _db.Doctors
                .Include(d => d.User)
                .Where(d => !d.User.IsSoftDeleted);

            if (!string.IsNullOrWhiteSpace(clinicId))
                query = query.Where(d => d.User.ClinicId == clinicId);

            var doctors = await query
                .Select(d => new
                {
                    d.Id,
                    DisplayName = "Dr. " + (d.User.FullName ?? d.User.Email) + " - " + (d.Specialty ?? "General Practice")
                })
                .ToListAsync();

            DoctorList = new SelectList(doctors, "Id", "DisplayName");
        }

        private static string SanitizeFileName(string name, string fallbackExt)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "file" + fallbackExt;

            var cleaned = new string(name
                .Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                .ToArray());

            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.StartsWith('.'))
                cleaned = "file" + (string.IsNullOrEmpty(Path.GetExtension(cleaned)) ? fallbackExt : "");

            return cleaned.Length > 80 ? cleaned[^80..] : cleaned;
        }
    }
}
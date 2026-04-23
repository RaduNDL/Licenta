using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;

namespace Licenta.Pages.Doctor.Attachments
{
    [Authorize(Roles = "Doctor")]
    public class UploadModel : PageModel
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".dcm", ".tif", ".tiff"
        };

        private const long MaxFileSizeBytes = 20L * 1024 * 1024;

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public UploadModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public class InputModel
        {
            public Guid PatientId { get; set; }
            public string Type { get; set; } = string.Empty;
            public IFormFile File { get; set; } = default!;
        }

        [BindProperty]
        public InputModel ModelInput { get; set; } = new();

        public SelectList Patients { get; set; } = default!;

        public async Task OnGetAsync(Guid? patientId)
        {
            await LoadPatientsAsync(patientId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || ModelInput.File == null)
            {
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            if (ModelInput.File.Length == 0)
            {
                ModelState.AddModelError("ModelInput.File", "The file is empty.");
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            if (ModelInput.File.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError("ModelInput.File",
                    $"File is too large. Max size is {MaxFileSizeBytes / (1024 * 1024)} MB.");
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            var ext = Path.GetExtension(ModelInput.File.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("ModelInput.File",
                    "Unsupported file type. Allowed: PDF, JPG/JPEG, PNG, DCM, TIFF.");
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                ModelState.AddModelError(string.Empty, "Doctor not found.");
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            var patient = await _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == ModelInput.PatientId);

            if (patient == null)
            {
                ModelState.AddModelError(string.Empty, "Patient not found.");
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            var clinicId = (user.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                var patientClinicId = (patient.User?.ClinicId ?? "").Trim();
                if (patientClinicId != clinicId)
                    return Forbid();
            }

            var uploadsRoot = Path.Combine(
                _env.ContentRootPath,
                "Files", "uploads", "doctor", doctor.Id.ToString());

            Directory.CreateDirectory(uploadsRoot);

            var originalName = Path.GetFileName(ModelInput.File.FileName);
            var sanitized = SanitizeFileName(originalName, fallbackExt: ext);
            var fileName = $"{Guid.NewGuid():N}_{sanitized}";
            var absolutePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = System.IO.File.Create(absolutePath))
            {
                await ModelInput.File.CopyToAsync(stream);
            }

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = ModelInput.PatientId,
                DoctorId = doctor.Id,
                FileName = originalName,
                FilePath = absolutePath,                       
                Type = string.IsNullOrWhiteSpace(ModelInput.Type) ? "Medical Document" : ModelInput.Type.Trim(),
                UploadedAt = DateTime.UtcNow,
                ContentType = ModelInput.File.ContentType,
                Status = AttachmentStatus.Validated,
                ValidatedAtUtc = DateTime.UtcNow,
                ValidatedByDoctorId = doctor.Id
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Doctor/Attachments/Index", new { patientId = ModelInput.PatientId });
        }

        private async Task LoadPatientsAsync(Guid? selectedId)
        {
            var user = await _userManager.GetUserAsync(User);
            var clinicId = (user?.ClinicId ?? "").Trim();

            var q = _db.Patients.Include(p => p.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(clinicId))
                q = q.Where(p => p.User.ClinicId == clinicId);

            var patients = await q
                .OrderBy(p => p.User.FullName ?? p.User.Email)
                .ToListAsync();

            Patients = new SelectList(
                patients,
                nameof(PatientProfile.Id),
                "User.FullName",
                selectedId);

            if (selectedId.HasValue)
                ModelInput.PatientId = selectedId.Value;
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
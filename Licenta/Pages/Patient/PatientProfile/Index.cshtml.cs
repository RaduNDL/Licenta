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
using PatientProfileEntity = Licenta.Models.PatientProfile;

namespace Licenta.Pages.Patient.PatientProfile
{
    [Authorize(Roles = "Patient")]
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

        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "Patient";
        public string DisplaySubtitle { get; set; } = "Patient";
        public string Email { get; set; } = "";
        public string ClinicId { get; set; } = "";
        public string? ProfileImageUrl { get; set; }

        public int StatsUpcomingAppointments { get; set; }
        public int StatsMedicalRecords { get; set; }
        public int StatsLabResults { get; set; }
        public int StatsUnreadMessages { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(80, MinimumLength = 2)]
            public string FullName { get; set; } = "";

            [StringLength(20)]
            public string? NationalId { get; set; }

            public DateTime? DateOfBirth { get; set; }

            [StringLength(30)]
            public string? Phone { get; set; }

            [StringLength(200)]
            public string? Address { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var patient = await EnsurePatientProfileAsync(user.Id);

            UserId = user.Id;
            Email = user.Email ?? user.UserName ?? "";
            ClinicId = user.ClinicId ?? "";
            DisplayName = user.FullName ?? user.Email ?? "Patient";

            Input = new InputModel
            {
                FullName = user.FullName ?? "",
                NationalId = patient.NationalId,
                DateOfBirth = patient.DateOfBirth,
                Phone = patient.Phone,
                Address = patient.Address
            };

            DisplaySubtitle = string.IsNullOrWhiteSpace(Input.Phone) ? "Patient" : Input.Phone;

            ProfileImageUrl = await GetPatientProfilePhotoUrlAsync(patient.Id);

            await LoadStatsAsync(user.Id, patient.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var patient = await EnsurePatientProfileAsync(user.Id);

            if (!ModelState.IsValid)
            {
                await HydrateHeaderAsync(user.Id, patient.Id);
                return Page();
            }

            user.FullName = (Input.FullName ?? "").Trim();

            var updUser = await _userManager.UpdateAsync(user);
            if (!updUser.Succeeded)
            {
                foreach (var e in updUser.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                await HydrateHeaderAsync(user.Id, patient.Id);
                return Page();
            }

            patient.NationalId = string.IsNullOrWhiteSpace(Input.NationalId) ? null : Input.NationalId.Trim();
            patient.DateOfBirth = Input.DateOfBirth;
            patient.Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
            patient.Address = string.IsNullOrWhiteSpace(Input.Address) ? null : Input.Address.Trim();

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Profile updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUploadPhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var patient = await EnsurePatientProfileAsync(user.Id);

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

            var folder = Path.Combine(_env.WebRootPath, "uploads", "patient", patient.Id.ToString(), "profile");
            Directory.CreateDirectory(folder);

            var fileName = $"patient_{patient.Id:N}_{Guid.NewGuid():N}{ext}";
            var absPath = Path.Combine(folder, fileName);

            using (var fs = new FileStream(absPath, FileMode.Create))
                await PhotoFile.CopyToAsync(fs);

            await RemoveExistingProfilePhotosAsync(patient.Id);

            var relPath = $"/uploads/patient/{patient.Id}/profile/{fileName}";

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = null,
                FileName = fileName,
                FilePath = relPath,
                ContentType = PhotoFile.ContentType ?? "application/octet-stream",
                Type = "ProfilePhoto",
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Validated,
                PatientNotes = null,
                ValidationNotes = null
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Photo updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemovePhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var patient = await EnsurePatientProfileAsync(user.Id);

            await RemoveExistingProfilePhotosAsync(patient.Id);

            TempData["StatusMessage"] = "Photo removed.";
            return RedirectToPage();
        }

        private async Task<PatientProfileEntity> EnsurePatientProfileAsync(string userId)
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient != null)
                return patient;

            var created = new PatientProfileEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };

            _db.Patients.Add(created);
            await _db.SaveChangesAsync();
            return created;
        }

        private async Task HydrateHeaderAsync(string userId, Guid patientId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                UserId = user.Id;
                Email = user.Email ?? user.UserName ?? "";
                ClinicId = user.ClinicId ?? "";
                DisplayName = user.FullName ?? user.Email ?? "Patient";
            }

            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId);
            if (patient != null)
            {
                Input ??= new InputModel();
                Input.NationalId = patient.NationalId;
                Input.DateOfBirth = patient.DateOfBirth;
                Input.Phone = patient.Phone;
                Input.Address = patient.Address;

                DisplaySubtitle = string.IsNullOrWhiteSpace(Input.Phone) ? "Patient" : Input.Phone;

                ProfileImageUrl = await GetPatientProfilePhotoUrlAsync(patientId);

                await LoadStatsAsync(userId, patientId);
            }
        }

        private async Task LoadStatsAsync(string userId, Guid patientId)
        {
            var now = DateTime.Now;

            StatsUpcomingAppointments = await _db.Appointments.AsNoTracking()
                .Where(a => a.PatientId == patientId && a.Status != AppointmentStatus.Cancelled && a.ScheduledAt >= now)
                .CountAsync();

            StatsMedicalRecords = await _db.MedicalRecords.AsNoTracking()
                .Where(r => r.PatientId == patientId)
                .CountAsync();

            StatsLabResults = await _db.LabResults.AsNoTracking()
                .Where(l => l.PatientId == patientId)
                .CountAsync();

            StatsUnreadMessages = await _db.InternalMessages.AsNoTracking()
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .CountAsync();
        }

        private async Task<string?> GetPatientProfilePhotoUrlAsync(Guid patientId)
        {
            return await _db.MedicalAttachments.AsNoTracking()
                .Where(a => a.PatientId == patientId && a.Type == "ProfilePhoto")
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => a.FilePath)
                .FirstOrDefaultAsync();
        }

        private async Task RemoveExistingProfilePhotosAsync(Guid patientId)
        {
            var old = await _db.MedicalAttachments
                .Where(a => a.PatientId == patientId && a.Type == "ProfilePhoto")
                .ToListAsync();

            if (old.Count == 0)
                return;

            foreach (var a in old)
            {
                if (!string.IsNullOrWhiteSpace(a.FilePath))
                    TryDeleteOldPhoto(a.FilePath);
            }

            _db.MedicalAttachments.RemoveRange(old);
            await _db.SaveChangesAsync();
        }

        private void TryDeleteOldPhoto(string webPath)
        {
            try
            {
                if (!webPath.StartsWith("/uploads/patient/", StringComparison.OrdinalIgnoreCase))
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

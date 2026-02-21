using Licenta.Areas.Identity.Data;
using Licenta.Models;
using DoctorProfileEntity = Licenta.Models.DoctorProfile;
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

namespace Licenta.Pages.Doctor.DoctorProfile
{
    [Authorize(Roles = "Doctor")]
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

        public string DisplayName { get; set; } = "Doctor";
        public string DisplaySubtitle { get; set; } = "Doctor";
        public string Email { get; set; } = "";
        public string ClinicId { get; set; } = "";
        public string? ProfileImageUrl { get; set; }

        public int StatsAppointments { get; set; }
        public int StatsPatients { get; set; }
        public int StatsPendingAttachments { get; set; }
        public int StatsUnreadMessages { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(80, MinimumLength = 2)]
            public string FullName { get; set; } = "";

            [Phone]
            [StringLength(30)]
            public string? PhoneNumber { get; set; }

            [StringLength(80)]
            public string? Specialty { get; set; }

            [StringLength(50)]
            public string? LicenseNumber { get; set; }

            [StringLength(200)]
            public string? Languages { get; set; }

            [StringLength(2000)]
            public string? Bio { get; set; }

            [StringLength(150)]
            public string? OfficeAddress { get; set; }

            [StringLength(80)]
            public string? City { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await EnsureDoctorProfileAsync(user.Id);

            Email = user.Email ?? user.UserName ?? "";
            ClinicId = user.ClinicId ?? "";
            DisplayName = user.FullName ?? user.Email ?? "Doctor";

            Input = new InputModel
            {
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber,

                Specialty = doctor.Specialty,
                LicenseNumber = doctor.LicenseNumber,
                Languages = doctor.Languages,
                Bio = doctor.Bio,
                OfficeAddress = doctor.OfficeAddress,
                City = doctor.City
            };

            ProfileImageUrl = doctor.ProfileImagePath;
            DisplaySubtitle = string.IsNullOrWhiteSpace(Input.Specialty) ? "Doctor" : Input.Specialty;

            await LoadStatsAsync(user.Id, doctor.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await EnsureDoctorProfileAsync(user.Id);

            if (!ModelState.IsValid)
            {
                await HydrateHeaderAsync(user.Id, doctor.Id);
                return Page();
            }

            user.FullName = (Input.FullName ?? "").Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();

            var updUser = await _userManager.UpdateAsync(user);
            if (!updUser.Succeeded)
            {
                foreach (var e in updUser.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                await HydrateHeaderAsync(user.Id, doctor.Id);
                return Page();
            }

            doctor.Specialty = string.IsNullOrWhiteSpace(Input.Specialty) ? null : Input.Specialty.Trim();
            doctor.LicenseNumber = string.IsNullOrWhiteSpace(Input.LicenseNumber) ? null : Input.LicenseNumber.Trim();
            doctor.Languages = string.IsNullOrWhiteSpace(Input.Languages) ? null : Input.Languages.Trim();
            doctor.Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();
            doctor.OfficeAddress = string.IsNullOrWhiteSpace(Input.OfficeAddress) ? null : Input.OfficeAddress.Trim();
            doctor.City = string.IsNullOrWhiteSpace(Input.City) ? null : Input.City.Trim();

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Profile updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUploadPhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await EnsureDoctorProfileAsync(user.Id);

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

            var folder = Path.Combine(_env.WebRootPath, "uploads", "doctors");
            Directory.CreateDirectory(folder);

            var fileName = $"doctor_{doctor.Id:N}_{Guid.NewGuid():N}{ext}";
            var absPath = Path.Combine(folder, fileName);

            using (var fs = new FileStream(absPath, FileMode.Create))
                await PhotoFile.CopyToAsync(fs);

            if (!string.IsNullOrWhiteSpace(doctor.ProfileImagePath))
                TryDeleteOldPhoto(doctor.ProfileImagePath);

            doctor.ProfileImagePath = "/uploads/doctors/" + fileName;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Photo updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemovePhotoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await EnsureDoctorProfileAsync(user.Id);

            if (!string.IsNullOrWhiteSpace(doctor.ProfileImagePath))
            {
                TryDeleteOldPhoto(doctor.ProfileImagePath);
                doctor.ProfileImagePath = null;
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Photo removed.";
            return RedirectToPage();
        }

        private async Task<DoctorProfileEntity> EnsureDoctorProfileAsync(string userId)
        {
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor != null)
                return doctor;

            var created = new DoctorProfileEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };

            _db.Doctors.Add(created);
            await _db.SaveChangesAsync();
            return created;
        }

        private async Task HydrateHeaderAsync(string userId, Guid doctorId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                Email = user.Email ?? user.UserName ?? "";
                ClinicId = user.ClinicId ?? "";
                DisplayName = user.FullName ?? user.Email ?? "Doctor";
            }

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId);
            if (doctor != null)
            {
                ProfileImageUrl = doctor.ProfileImagePath;
                DisplaySubtitle = string.IsNullOrWhiteSpace(doctor.Specialty) ? "Doctor" : doctor.Specialty;
                await LoadStatsAsync(userId, doctorId);
            }
        }

        private async Task LoadStatsAsync(string userId, Guid doctorId)
        {
            StatsAppointments = await _db.Appointments.AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.Status != AppointmentStatus.Cancelled)
                .CountAsync();

            StatsPatients = await _db.Patients.AsNoTracking()
                .Where(p => p.User != null && (p.User.ClinicId ?? "") == (ClinicId ?? ""))
                .CountAsync();

            StatsPendingAttachments = await _db.MedicalAttachments.AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.Status == AttachmentStatus.Pending && a.Type != "AppointmentRequest")
                .CountAsync();

            StatsUnreadMessages = await _db.InternalMessages.AsNoTracking()
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .CountAsync();
        }

        private void TryDeleteOldPhoto(string webPath)
        {
            try
            {
                if (!webPath.StartsWith("/uploads/doctors/", StringComparison.OrdinalIgnoreCase))
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

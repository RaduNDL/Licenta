using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Attachments
{
    [Authorize(Roles = "Doctor")]
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

            var user = await _userManager.GetUserAsync(User);
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                ModelState.AddModelError(string.Empty, "Doctor not found.");
                await LoadPatientsAsync(ModelInput.PatientId);
                return Page();
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "doctor", doctor.Id.ToString());
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid()}_{ModelInput.File.FileName}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await ModelInput.File.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/doctor/{doctor.Id}/{fileName}";

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = ModelInput.PatientId,
                DoctorId = doctor.Id,
                FileName = ModelInput.File.FileName,
                FilePath = relativePath,
                Type = ModelInput.Type,
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
            var patients = await _db.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName ?? p.User.Email)
                .ToListAsync();

            Patients = new SelectList(
                patients,
                nameof(PatientProfile.Id),
                "User.FullName",
                selectedId
            );

            if (selectedId.HasValue)
            {
                ModelInput.PatientId = selectedId.Value;
            }
        }
    }
}
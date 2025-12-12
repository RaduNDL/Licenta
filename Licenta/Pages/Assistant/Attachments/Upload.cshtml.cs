using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Attachments
{
    [Authorize(Roles = "Assistant")]
    public class UploadModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public UploadModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
            _notifier = notifier;
        }

        public class InputModel
        {
            [Required]
            public Guid PatientId { get; set; }

            public string Type { get; set; } = string.Empty;

            public string? Description { get; set; }

            [Required]
            public IFormFile File { get; set; } = default!;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList Patients { get; set; } = default!;

        public async Task OnGetAsync(Guid? patientId)
        {
            await LoadPatientsAsync(patientId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || Input.File == null)
            {
                await LoadPatientsAsync(Input.PatientId);
                return Page();
            }

            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return Page();
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid()}_{Input.File.FileName}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await Input.File.CopyToAsync(stream);
            }

            var doctor = await _db.Doctors
                .Include(d => d.User)
                .OrderBy(d => d.User.FullName)
                .FirstOrDefaultAsync();

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = Input.PatientId,
                DoctorId = doctor?.Id ?? Guid.Empty,
                FileName = Input.File.FileName,
                FilePath = "/uploads/" + fileName,
                Type = Input.Type,
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Pending,
                ValidationNotes = Input.Description,
                UploadedByAssistantId = assistant.Id
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            var patient = await _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == Input.PatientId);

            if (patient?.User != null)
            {
                await _notifier.NotifyAsync(
                    patient.User,
                    NotificationType.Document,
                    "New document uploaded",
                    $"A new document of type <b>{Input.Type}</b> was uploaded for you.<br/>Status: <b>Pending validation</b>.",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString()
                );
            }

            if (doctor?.User != null)
            {
                await _notifier.NotifyAsync(
                    doctor.User,
                    NotificationType.Document,
                    "New document pending validation",
                    $"Patient: {patient?.User?.FullName ?? patient?.User?.Email}<br/>Type: {Input.Type}<br/>Please review and validate the document.",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString()
                );
            }

            TempData["StatusMessage"] = "Document uploaded successfully.";
            return RedirectToPage("/Assistant/Attachments/Index");
        }

        private async Task LoadPatientsAsync(Guid? selected)
        {
            var patients = await _db.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName ?? p.User.Email)
                .ToListAsync();

            Patients = new SelectList(patients, nameof(PatientProfile.Id), "User.FullName", selected);
        }
    }
}

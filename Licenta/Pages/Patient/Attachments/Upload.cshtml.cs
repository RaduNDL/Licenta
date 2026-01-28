using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
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
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Attachments
{
    [Authorize(Roles = "Patient")]
    public class UploadModel : PageModel
    {
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

        public class InputModel
        {
            [Required(ErrorMessage = "Please select a document type.")]
            public string Type { get; set; } = "";

            public string? Notes { get; set; }

            [Required(ErrorMessage = "Please select a file.")]
            public IFormFile File { get; set; } = null!;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var patient = await _db.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
                return Forbid();

            var uploadsFolder = Path.Combine(
                _env.WebRootPath,
                "uploads",
                "patient_docs");

            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(Input.File.FileName)}";
            var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

            await using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await Input.File.CopyToAsync(stream);
            }

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                FileName = Input.File.FileName,
                FilePath = $"/uploads/patient_docs/{uniqueFileName}",
                ContentType = Input.File.ContentType,
                Type = Input.Type,
                PatientNotes = Input.Notes,
                Status = AttachmentStatus.Pending,
                UploadedAt = DateTime.UtcNow
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            var doctors = await _userManager.GetUsersInRoleAsync("Doctor");

            foreach (var doctor in doctors)
            {
                if (!string.IsNullOrWhiteSpace(user.ClinicId) &&
                    doctor.ClinicId != user.ClinicId)
                    continue;

                await _notifier.NotifyAsync(
                    doctor,
                    NotificationType.Document,
                    "New medical document uploaded",
                    $"A patient uploaded a new document:<br/><b>{attachment.FileName}</b>",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString(),
                    sendEmail: false
                );
            }

            TempData["StatusMessage"] =
                "Document uploaded successfully. Your doctor has been notified.";

            return RedirectToPage("/Patient/Attachments/Index");
        }
    }
}

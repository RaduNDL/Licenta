using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Form
{
    [Authorize(Roles = "Patient")]
    public class PreConsultationModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public PreConsultationModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, INotificationService notifier)
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
            [Required, MaxLength(200)]
            public string MainComplaint { get; set; } = string.Empty;

            [Required, MaxLength(4000)]
            public string Symptoms { get; set; } = string.Empty;

            [MaxLength(1000)]
            public string? Allergies { get; set; }

            [MaxLength(2000)]
            public string? Medications { get; set; }

            [MaxLength(2000)]
            public string? Notes { get; set; }
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Please fix errors.";
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null)
            {
                TempData["StatusMessage"] = "Patient profile not found.";
                return Page();
            }

            var payload = JsonSerializer.Serialize(new
            {
                CreatedAtUtc = DateTime.UtcNow,
                PatientUserId = user.Id,
                MainComplaint = Input.MainComplaint,
                Symptoms = Input.Symptoms,
                Allergies = Input.Allergies,
                Medications = Input.Medications,
                Notes = Input.Notes
            });

            var folder = Path.Combine(_env.WebRootPath, "uploads", "patient", patient.Id.ToString(), "forms");
            Directory.CreateDirectory(folder);

            var safeName = $"preconsult_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.json";
            var fullPath = Path.Combine(folder, safeName);
            await System.IO.File.WriteAllTextAsync(fullPath, payload);

            var relPath = $"/uploads/patient/{patient.Id}/forms/{safeName}";

            var att = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = null,
                FileName = safeName,
                FilePath = relPath,
                ContentType = "application/json",
                Type = "PreConsultationForm",
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Pending,
                ValidationNotes = null,
                AssignedByAssistantId = null,
                ValidatedByDoctorId = null
            };

            _db.MedicalAttachments.Add(att);
            await _db.SaveChangesAsync();

            var patientName = user.FullName ?? user.Email ?? user.UserName;

            var assistants = await _userManager.GetUsersInRoleAsync("Assistant");
            foreach (var assistant in assistants)
            {
                await _notifier.NotifyAsync(
                    assistant,
                    NotificationType.Info,
                    "New pre-consultation form",
                    $"Patient <b>{patientName}</b> submitted a pre-consultation form. Please assign a doctor.",
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: att.Id.ToString()
                );
            }

            await _notifier.NotifyAsync(
                user,
                NotificationType.Info,
                "Pre-consultation form submitted",
                "Your pre-consultation form was submitted and is pending assistant review.",
                relatedEntity: "MedicalAttachment",
                relatedEntityId: att.Id.ToString(),
                sendEmail: false
            );

            TempData["StatusMessage"] = "Form submitted. A medical assistant will assign a doctor.";
            return RedirectToPage();
        }
    }
}

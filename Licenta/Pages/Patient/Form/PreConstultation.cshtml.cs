using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Forms
{
    [Authorize(Roles = "Patient")]
    public class PreConsultationModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public PreConsultationModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public SelectList Doctors { get; set; } = default!;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public Guid DoctorId { get; set; }

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

        public async Task OnGetAsync()
        {
            var doctors = await _db.Doctors
                .Include(d => d.User)
                .OrderBy(d => d.User.FullName ?? d.User.Email)
                .ToListAsync();

            Doctors = new SelectList(doctors, "Id", "User.FullName");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await OnGetAsync(); 

            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Please fix errors.";
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return Page();
            }

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
            }, new JsonSerializerOptions { WriteIndented = true });

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
                DoctorId = Input.DoctorId,
                FileName = safeName,
                FilePath = relPath,
                ContentType = "application/json",
                Type = "PreConsultationForm",
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Pending,
                ValidationNotes = null
            };

            _db.MedicalAttachments.Add(att);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Form submitted for review.";
            return RedirectToPage();
        }
    }
}

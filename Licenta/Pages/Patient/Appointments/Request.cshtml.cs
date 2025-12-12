using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient.Appointments
{
    [Authorize(Roles = "Patient")]
    public class RequestModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public RequestModel(
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

        public SelectList Doctors { get; set; } = default!;
        public List<DoctorAvailability> DoctorSchedule { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public Guid DoctorId { get; set; }

            [Required]
            public DateTime PreferredDate { get; set; } = DateTime.UtcNow.Date;

            [Required, MaxLength(100)]
            public string? PreferredTime { get; set; }

            [Required, MaxLength(2000)]
            public string Reason { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(Guid? doctorId)
        {
            await LoadDoctorsAndScheduleAsync(doctorId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDoctorsAndScheduleAsync(Input.DoctorId);

            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Please fix form errors.";
                return Page();
            }

            // 🔴 VALIDARE NOUĂ: data + ora trebuie să fie în orarul doctorului
            if (DoctorSchedule == null || DoctorSchedule.Count == 0)
            {
                ModelState.AddModelError(nameof(Input.PreferredDate),
                    "This doctor has no schedule defined. You cannot request an appointment.");
                TempData["StatusMessage"] = "This doctor has no schedule defined.";
                return Page();
            }

            var date = Input.PreferredDate.Date;

            if (!TimeSpan.TryParse(Input.PreferredTime, out var preferredTimeSpan))
            {
                ModelState.AddModelError(nameof(Input.PreferredTime), "Invalid time format.");
                TempData["StatusMessage"] = "Please select a valid time.";
                return Page();
            }

            // toate intervalele active pentru ziua respectivă
            var dayOfWeek = date.DayOfWeek;
            var daySlots = DoctorSchedule
                .Where(a => a.IsActive && a.DayOfWeek == dayOfWeek)
                .ToList();

            if (!daySlots.Any())
            {
                ModelState.AddModelError(nameof(Input.PreferredDate),
                    "The selected date is outside the doctor's working days.");
                TempData["StatusMessage"] =
                    "You can only request appointments on days when the doctor is scheduled to work.";
                return Page();
            }

            var insideWorkingHours = daySlots.Any(a =>
                preferredTimeSpan >= a.StartTime && preferredTimeSpan < a.EndTime);

            if (!insideWorkingHours)
            {
                ModelState.AddModelError(nameof(Input.PreferredTime),
                    "The selected time is outside the doctor's working hours for this day.");
                TempData["StatusMessage"] =
                    "Please choose a time within the doctor's working hours shown above.";
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
                patient = new PatientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id
                };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync();
            }

            var payload = JsonSerializer.Serialize(new
            {
                CreatedAtUtc = DateTime.UtcNow,
                PatientUserId = user.Id,
                PreferredDate = Input.PreferredDate,
                PreferredTime = Input.PreferredTime,
                Reason = Input.Reason
            }, new JsonSerializerOptions { WriteIndented = true });

            var folder = Path.Combine(_env.WebRootPath, "uploads", "patient", patient.Id.ToString(), "requests");
            Directory.CreateDirectory(folder);

            var safeName = $"appointment_request_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.json";
            var fullPath = Path.Combine(folder, safeName);
            await System.IO.File.WriteAllTextAsync(fullPath, payload);

            var relPath = $"/uploads/patient/{patient.Id}/requests/{safeName}";

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = Input.DoctorId,
                FileName = safeName,
                FilePath = relPath,
                ContentType = "application/json",
                Type = "AppointmentRequest",
                UploadedAt = DateTime.UtcNow,
                Status = AttachmentStatus.Pending,
                Description = $"Appointment request for {Input.PreferredDate:yyyy-MM-dd} {Input.PreferredTime}"
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            var patientName = user.FullName ?? user.Email ?? user.UserName;

            var doctorProfile = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == Input.DoctorId);

            if (doctorProfile?.User != null)
            {
                var title = "New appointment request";
                var body =
                    $"Patient <b>{patientName}</b> sent a new appointment request for " +
                    $"<b>{Input.PreferredDate:yyyy-MM-dd} {Input.PreferredTime}</b>.";

                await _notifier.NotifyAsync(
                    doctorProfile.User,
                    NotificationType.Info,
                    title,
                    body,
                    relatedEntity: "MedicalAttachment",
                    relatedEntityId: attachment.Id.ToString());
            }

            await _notifier.NotifyAsync(
                user,
                NotificationType.Info,
                "Appointment request submitted",
                $"Your appointment request for <b>{Input.PreferredDate:yyyy-MM-dd} {Input.PreferredTime}</b> was sent and is waiting for review.",
                relatedEntity: "MedicalAttachment",
                relatedEntityId: attachment.Id.ToString(),
                sendEmail: false);

            TempData["StatusMessage"] = "Request sent. You will be notified when it is reviewed.";

            return RedirectToPage(new { doctorId = Input.DoctorId });
        }

        private async Task LoadDoctorsAndScheduleAsync(Guid? selectedDoctorId)
        {
            var doctorUsers = await _userManager.GetUsersInRoleAsync("Doctor");

            foreach (var user in doctorUsers)
            {
                bool hasProfile = await _db.Doctors.AnyAsync(d => d.UserId == user.Id);
                if (!hasProfile)
                {
                    _db.Doctors.Add(new DoctorProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id
                    });
                }
            }

            if (_db.ChangeTracker.HasChanges())
            {
                await _db.SaveChangesAsync();
            }

            var doctors = await _db.Doctors
                .Include(d => d.User)
                .OrderBy(d => d.User.FullName ?? d.User.Email)
                .Select(d => new
                {
                    d.Id,
                    DisplayName = d.User.FullName ?? d.User.Email ?? "(no name)"
                })
                .ToListAsync();

            Guid? effectiveDoctorId = selectedDoctorId;
            if (effectiveDoctorId == null || effectiveDoctorId == Guid.Empty)
            {
                if (doctors.Count > 0)
                    effectiveDoctorId = doctors[0].Id;
            }

            Doctors = new SelectList(doctors, "Id", "DisplayName", effectiveDoctorId);

            DoctorSchedule = new List<DoctorAvailability>();
            if (effectiveDoctorId.HasValue && effectiveDoctorId.Value != Guid.Empty)
            {
                Input.DoctorId = effectiveDoctorId.Value;

                DoctorSchedule = await _db.DoctorAvailabilities
                    .Where(a => a.DoctorId == effectiveDoctorId.Value && a.IsActive)
                    .OrderBy(a => a.DayOfWeek)
                    .ToListAsync();
            }
        }
    }
}

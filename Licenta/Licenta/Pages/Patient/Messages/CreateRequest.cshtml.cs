using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Messages
{
    [Authorize(Roles = "Patient")]
    public class CreateRequestModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public CreateRequestModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        public class InputModel
        {
            [Required]
            public Guid DoctorId { get; set; }

            [Required]
            [MaxLength(200)]
            public string Subject { get; set; } = string.Empty;

            [Required]
            [MaxLength(4000)]
            public string Body { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList DoctorList { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDoctorsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var patientUser = await _userManager.GetUserAsync(User);
            if (patientUser == null) return Unauthorized();

            if (!ModelState.IsValid)
            {
                await LoadDoctorsAsync();
                return Page();
            }

            var patientProfileId = await _db.Patients
                .Where(p => p.UserId == patientUser.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

            if (patientProfileId == null) return BadRequest();

            var doctorProfile = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == Input.DoctorId);

            if (doctorProfile == null) return BadRequest();

            var req = new PatientMessageRequest
            {
                Id = Guid.NewGuid(),
                PatientId = patientProfileId.Value,
                DoctorProfileId = doctorProfile.Id,
                AssistantId = null,
                Subject = Input.Subject,
                Body = Input.Body,
                Status = PatientMessageRequestStatus.WaitingDoctorApproval,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.PatientMessageRequests.Add(req);
            await _db.SaveChangesAsync();

            await _notifier.NotifyAsync(
                doctorProfile.User,
                NotificationType.Message,
                $"New patient request: {req.Subject}",
                $"Patient {patientUser.FullName ?? patientUser.Email} sent a new message request.",
                actionUrl: "/Doctor/Messages/Requests",
                actionText: "View Requests",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );

            TempData["StatusMessage"] = "Request submitted. Waiting for approval.";
            return RedirectToPage("/Patient/Messages/RequestList");
        }

        private async Task LoadDoctorsAsync()
        {
            var doctors = await _db.Doctors
                .Include(d => d.User)
                .Where(d => !d.User.IsSoftDeleted)
                .Select(d => new
                {
                    d.Id,
                    DisplayName = "Dr. " + (d.User.FullName ?? d.User.Email) + " - " + (d.Specialty ?? "General Practice")
                })
                .ToListAsync();

            DoctorList = new SelectList(doctors, "Id", "DisplayName");
        }
    }
}
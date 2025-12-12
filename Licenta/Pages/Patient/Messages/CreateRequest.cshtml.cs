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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Pages.Patient.Messages
{
    [Authorize(Roles = "Patient")]
    public class CreateRequestModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public CreateRequestModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        public class InputModel
        {
            [Required]
            public string DoctorId { get; set; } = null!;

            [Required, MaxLength(200)]
            public string Subject { get; set; } = string.Empty;

            [Required, MaxLength(4000)]
            public string Body { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList Doctors { get; set; } = default!;

        public async Task OnGetAsync(string? doctorId)
        {
            await LoadDoctorsAsync(doctorId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDoctorsAsync(Input.DoctorId);
                return Page();
            }

            var patient = await _userManager.GetUserAsync(User);
            if (patient == null)
                return Unauthorized();

            var doctor = await _db.Users.FirstOrDefaultAsync(u => u.Id == Input.DoctorId);
            if (doctor == null)
            {
                ModelState.AddModelError(nameof(Input.DoctorId), "Selected doctor does not exist.");
                await LoadDoctorsAsync(Input.DoctorId);
                return Page();
            }

            var request = new PatientMessageRequest
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = Input.DoctorId,
                Subject = Input.Subject,
                Body = Input.Body,
                CreatedAt = DateTime.UtcNow,
                Status = PatientMessageRequestStatus.Pending
            };

            _db.PatientMessageRequests.Add(request);
            await _db.SaveChangesAsync();

            var patientName = patient.FullName ?? patient.Email ?? patient.UserName;
            var doctorName = doctor.FullName ?? doctor.Email ?? doctor.UserName;

            var title = "New patient message request";
            var msg =
                $"Patient <b>{patientName}</b> requested permission to contact doctor " +
                $"<b>{doctorName}</b>.<br/>" +
                $"Subject: <b>{Input.Subject}</b>.";

            var assistants = await _userManager.GetUsersInRoleAsync("Assistant");
            foreach (var assistant in assistants)
            {
                await _notifier.NotifyAsync(
                    assistant,
                    NotificationType.Info,
                    title,
                    msg,
                    relatedEntity: "PatientMessageRequest",
                    relatedEntityId: request.Id.ToString());
            }

            await _notifier.NotifyAsync(
                patient,
                NotificationType.Info,
                "Message request submitted",
                "Your message request was sent to the medical assistant and is pending review.",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: request.Id.ToString(),
                sendEmail: false);

            TempData["MessageRequestSuccess"] = "Your request was sent to the medical assistant and is pending review.";
            return RedirectToPage("/Patient/Messages/RequestList");
        }

        private async Task LoadDoctorsAsync(string? preselectId)
        {
            var doctorsInRole = await _userManager.GetUsersInRoleAsync("Doctor");

            var doctors = doctorsInRole
                .OrderBy(u => u.FullName ?? u.Email)
                .Select(u => new
                {
                    u.Id,
                    Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName
                })
                .ToList();

            Doctors = new SelectList(
                doctors,
                "Id",
                "Name",
                preselectId
            );
        }
    }
}

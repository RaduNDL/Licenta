using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
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
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.MaxLength(200)]
            public string Subject { get; set; } = string.Empty;

            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.MaxLength(4000)]
            public string Body { get; set; } = string.Empty;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var patient = await _userManager.GetUserAsync(User);
            if (patient == null) return Unauthorized();

            var clinicId = (patient.ClinicId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(clinicId))
            {
                TempData["StatusMessage"] = "Your account is not linked to a clinic.";
                return RedirectToPage("/Patient/Messages/RequestList");
            }

            var assistantRoleId = await _db.Roles
                .Where(r => r.Name == "Assistant")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(assistantRoleId))
            {
                TempData["StatusMessage"] = "Assistant role not found.";
                return RedirectToPage("/Patient/Messages/RequestList");
            }

            var clinicAssistants = await _db.Users
                .Where(u =>
                    !u.IsSoftDeleted &&
                    (u.ClinicId ?? "").Trim() == clinicId &&
                    _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == assistantRoleId))
                .ToListAsync();

            if (!clinicAssistants.Any())
            {
                TempData["StatusMessage"] = "No assistant available in your clinic.";
                return RedirectToPage("/Patient/Messages/RequestList");
            }

          
            var hasOpen = await _db.PatientMessageRequests
                .AnyAsync(r => r.PatientId == patient.Id && r.Status != PatientMessageRequestStatus.Closed);

            if (hasOpen)
            {
                TempData["StatusMessage"] = "You already have an open request.";
                return RedirectToPage("/Patient/Messages/RequestList");
            }

            var openCounts = await _db.PatientMessageRequests
                .Where(r => r.Status != PatientMessageRequestStatus.Closed)
                .GroupBy(r => r.AssistantId)
                .Select(g => new { AssistantId = g.Key, Cnt = g.Count() })
                .ToListAsync();

            var chosenAssistant = clinicAssistants
                .Select(a => new
                {
                    User = a,
                    Cnt = openCounts.FirstOrDefault(x => x.AssistantId == a.Id)?.Cnt ?? 0
                })
                .OrderBy(x => x.Cnt)
                .First()
                .User;

            var req = new PatientMessageRequest
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                AssistantId = chosenAssistant.Id,
                Subject = Input.Subject.Trim(),
                Body = Input.Body.Trim(),
                Status = PatientMessageRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.PatientMessageRequests.Add(req);
            await _db.SaveChangesAsync();

            await _notifier.NotifyAsync(
                chosenAssistant,
                NotificationType.Message,
                "New patient request",
                $"New request from <b>{patient.FullName ?? patient.Email}</b><br/>Subject: <b>{req.Subject}</b>",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );

            await _notifier.NotifyAsync(
                patient,
                NotificationType.Info,
                "Request submitted",
                "Your request was sent to an assistant and is waiting to be accepted.",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString(),
                sendEmail: false
            );

            TempData["StatusMessage"] = "Request submitted.";
            return RedirectToPage("/Patient/Messages/RequestList");
        }
    }
}

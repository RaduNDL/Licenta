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

namespace Licenta.Pages.Assistant.Messages.Requests
{
    [Authorize(Roles = "Assistant")]
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public ReviewModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        public Guid Id { get; set; }

        public string PatientName { get; set; } = "-";
        public string Subject { get; set; } = "-";
        public string Body { get; set; } = "-";
        public string Status { get; set; } = "-";
        public string AssistantName { get; set; } = "-";
        public string DoctorName { get; set; } = "-";
        public DateTime CreatedAtLocal { get; set; }

        public bool CanAccept { get; set; }
        public bool CanOpenAssistantChat { get; set; }
        public bool CanEscalate { get; set; }
        public bool CanReassign { get; set; }

        public SelectList Doctors { get; set; } = default!;

        public class EscalateInput
        {
            [Required]
            public string DoctorId { get; set; } = string.Empty;

            [Required]
            [MinLength(5)]
            [MaxLength(1000)]
            public string Reason { get; set; } = string.Empty;
        }

        [BindProperty]
        public EscalateInput Escalate { get; set; } = new();

        private async Task LoadDoctorsAsync(string? clinicId, string? selectedId)
        {
            var cid = (clinicId ?? "").Trim();

            var doctors = (await _userManager.GetUsersInRoleAsync("Doctor"))
                .Where(u =>
                    !u.IsSoftDeleted &&
                    (string.IsNullOrWhiteSpace(cid) || (u.ClinicId ?? "").Trim() == cid))
                .OrderBy(u => u.FullName ?? u.Email)
                .Select(u => new
                {
                    u.Id,
                    Name = string.IsNullOrWhiteSpace(u.FullName)
                        ? u.Email
                        : u.FullName
                })
                .ToList();

            Doctors = new SelectList(doctors, "Id", "Name", selectedId);
        }

        private async Task<PatientMessageRequest?> LoadAsync(Guid id)
        {
            return await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Assistant)
                .Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        private async Task FillAsync(
            PatientMessageRequest r,
            ApplicationUser assistant)
        {
            Id = r.Id;
            PatientName = r.Patient?.FullName ?? r.Patient?.Email ?? "-";
            Subject = r.Subject;
            Body = r.Body;
            Status = r.Status.ToString();
            AssistantName = r.Assistant?.FullName ?? r.Assistant?.Email ?? "-";
            DoctorName = r.Doctor?.FullName ?? r.Doctor?.Email ?? "-";
            CreatedAtLocal = r.CreatedAt.ToLocalTime();

            var isMine = r.AssistantId == assistant.Id;
            var isUnassigned = string.IsNullOrWhiteSpace(r.AssistantId);

            CanAccept =
                r.Status == PatientMessageRequestStatus.Pending &&
                (isUnassigned || isMine);

            CanOpenAssistantChat =
                r.Status == PatientMessageRequestStatus.AssistantChat &&
                isMine;

            CanEscalate =
                r.Status == PatientMessageRequestStatus.AssistantChat &&
                isMine;

            CanReassign =
                r.Status == PatientMessageRequestStatus.RejectedByDoctor &&
                isMine;

            await LoadDoctorsAsync(assistant.ClinicId, r.DoctorId);
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await LoadAsync(id);
            if (req == null)
                return NotFound();

            var clinicId = (assistant.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                var patientClinicId = (req.Patient?.ClinicId ?? "").Trim();
                if (patientClinicId != clinicId)
                    return Forbid();
            }

            if (req.Status != PatientMessageRequestStatus.Pending &&
                req.AssistantId != assistant.Id)
                return Forbid();

            await FillAsync(req, assistant);
            return Page();
        }

        public async Task<IActionResult> OnPostAcceptAsync(Guid id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await LoadAsync(id);
            if (req == null)
                return NotFound();

            var clinicId = (assistant.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                var patientClinicId = (req.Patient?.ClinicId ?? "").Trim();
                if (patientClinicId != clinicId)
                    return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(req.AssistantId) &&
                req.AssistantId != assistant.Id)
                return Forbid();

            req.AssistantId = assistant.Id;
            req.Status = PatientMessageRequestStatus.AssistantChat;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (req.Patient != null)
            {
                await _notifier.NotifyAsync(
                    req.Patient,
                    NotificationType.Message,
                    "Assistant opened the chat",
                    "Your request was accepted. You can now chat with the assistant.",
                    "PatientMessageRequest",
                    req.Id.ToString(),
                    false);
            }

            TempData["StatusMessage"] = "Chat opened.";

            return RedirectToPage(
                "/Assistant/Messages/Inbox",
                new { requestId = req.Id });
        }

        public async Task<IActionResult> OnPostEscalateAsync(Guid id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await LoadAsync(id);
            if (req == null)
                return NotFound();

            var clinicId = (assistant.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                var patientClinicId = (req.Patient?.ClinicId ?? "").Trim();
                if (patientClinicId != clinicId)
                    return Forbid();
            }

            if (req.AssistantId != assistant.Id)
                return Forbid();

            if (!ModelState.IsValid)
            {
                await FillAsync(req, assistant);
                return Page();
            }

            var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == Escalate.DoctorId);
            if (target == null) return NotFound();

            var isDoctor = await _userManager.IsInRoleAsync(target, "Doctor");
            if (!isDoctor) return Forbid();

            if (!string.IsNullOrWhiteSpace(clinicId) && (target.ClinicId ?? "").Trim() != clinicId)
                return Forbid();

            req.DoctorId = Escalate.DoctorId;
            req.EscalationReason = Escalate.Reason.Trim();
            req.Status = PatientMessageRequestStatus.WaitingDoctorApproval;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _notifier.NotifyAsync(
                target,
                NotificationType.Message,
                "New case awaiting approval",
                $"Assistant escalated a case.<br/><b>Reason:</b> {req.EscalationReason}",
                "PatientMessageRequest",
                req.Id.ToString());

            TempData["StatusMessage"] = "Sent to doctor.";

            return RedirectToPage("/Assistant/Messages/Requests/Index");
        }

        public async Task<IActionResult> OnPostReassignAsync(Guid id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await LoadAsync(id);
            if (req == null)
                return NotFound();

            var clinicId = (assistant.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                var patientClinicId = (req.Patient?.ClinicId ?? "").Trim();
                if (patientClinicId != clinicId)
                    return Forbid();
            }

            if (req.AssistantId != assistant.Id)
                return Forbid();

            if (!ModelState.IsValid)
            {
                await FillAsync(req, assistant);
                return Page();
            }

            var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == Escalate.DoctorId);
            if (target == null) return NotFound();

            var isDoctor = await _userManager.IsInRoleAsync(target, "Doctor");
            if (!isDoctor) return Forbid();

            if (!string.IsNullOrWhiteSpace(clinicId) && (target.ClinicId ?? "").Trim() != clinicId)
                return Forbid();

            req.DoctorId = Escalate.DoctorId;
            req.EscalationReason = Escalate.Reason.Trim();
            req.Status = PatientMessageRequestStatus.WaitingDoctorApproval;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _notifier.NotifyAsync(
                target,
                NotificationType.Message,
                "New case awaiting approval",
                $"A case was reassigned to you.<br/><b>Reason:</b> {req.EscalationReason}",
                "PatientMessageRequest",
                req.Id.ToString());

            TempData["StatusMessage"] = "Reassigned to doctor.";

            return RedirectToPage("/Assistant/Messages/Requests/Index");
        }


    }
}

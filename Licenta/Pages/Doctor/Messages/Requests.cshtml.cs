using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Messages;

[Authorize(Roles = "Doctor")]
public class RequestsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifier;

    public RequestsModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
    {
        _db = db;
        _userManager = userManager;
        _notifier = notifier;
    }

    public record RequestVm(Guid Id, string PatientName, string Subject, string Details, DateTime CreatedAtLocal);
    public record AssistantVm(string Id, string Name);

    public List<RequestVm> Requests { get; set; } = new();
    public List<AssistantVm> Assistants { get; set; } = new();

    public async Task OnGetAsync()
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return;

        var doctorProfile = await _db.Doctors
            .Include(d => d.Assistants)
            .FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);

        if (doctorProfile == null) return;

        Assistants = (doctorProfile.Assistants ?? new List<ApplicationUser>())
            .Where(a => !a.IsSoftDeleted)
            .Select(a => new AssistantVm(
                a.Id,
                string.IsNullOrWhiteSpace(a.FullName) ? (a.Email ?? "Assistant") : a.FullName))
            .ToList();

        Requests = await _db.PatientMessageRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .Where(r =>
                r.DoctorProfileId == doctorProfile.Id &&
                r.Status == PatientMessageRequestStatus.WaitingDoctorApproval)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RequestVm(
                r.Id,
                string.IsNullOrWhiteSpace(r.Patient.FullName)
                    ? (r.Patient.Email ?? "Patient")
                    : r.Patient.FullName,
                string.IsNullOrWhiteSpace(r.Subject)
                    ? "No subject"
                    : r.Subject,
                string.IsNullOrWhiteSpace(r.AssistantNote)
                    ? (string.IsNullOrWhiteSpace(r.EscalationReason)
                        ? (string.IsNullOrWhiteSpace(r.Body) ? "-" : r.Body)
                        : r.EscalationReason)
                    : r.AssistantNote,
                r.CreatedAt.ToLocalTime()
            ))
            .ToListAsync();

        Requests = Requests
            .Select(r => new RequestVm(
                r.Id,
                string.IsNullOrWhiteSpace(r.PatientName) ? "Patient" : r.PatientName.Trim(),
                string.IsNullOrWhiteSpace(r.Subject) ? "No subject" : r.Subject.Trim(),
                string.IsNullOrWhiteSpace(r.Details) ? "-" : r.Details.Trim(),
                r.CreatedAtLocal
            ))
            .ToList();
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfileId = await _db.Doctors
            .Where(d => d.UserId == doctorUser.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (doctorProfileId == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == doctorProfileId.Value);

        if (req == null) return NotFound();

        if (req.Status != PatientMessageRequestStatus.WaitingDoctorApproval)
            return RedirectToPage();

        req.Status = PatientMessageRequestStatus.ActiveDoctorChat;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Patient.UserId);
        if (patientUser != null)
        {
            await _notifier.NotifyAsync(
                patientUser,
                NotificationType.System,
                "Consultation Started",
                $"Dr. {doctorUser.FullName ?? doctorUser.Email} has accepted your message request.",
                actionUrl: $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Doctor",
                actionText: "Open Chat",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );
        }

        return RedirectToPage("/Doctor/Messages/Inbox", new { requestId = req.Id });
    }

    public async Task<IActionResult> OnPostDeclineAsync(Guid id)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfileId = await _db.Doctors
            .Where(d => d.UserId == doctorUser.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (doctorProfileId == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == doctorProfileId.Value);

        if (req == null) return NotFound();

        req.Status = PatientMessageRequestStatus.RejectedByDoctor;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Patient.UserId);
        if (patientUser != null)
        {
            await _notifier.NotifyAsync(
                patientUser,
                NotificationType.System,
                "Request Rejected",
                $"Dr. {doctorUser.FullName ?? doctorUser.Email} has declined your message request.",
                actionUrl: "/Patient/Messages/RequestList",
                actionText: "View Status",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString()
            );
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDelegateAsync(Guid id, string assistantId)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfile = await _db.Doctors
            .Include(d => d.Assistants)
            .FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);

        if (doctorProfile == null)
        {
            TempData["StatusMessage"] = "Doctor profile not found.";
            return RedirectToPage();
        }

        var availableAssistants = (doctorProfile.Assistants ?? new List<ApplicationUser>())
            .Where(a => !a.IsSoftDeleted)
            .ToList();

        if (!availableAssistants.Any())
        {
            TempData["StatusMessage"] = "You have no assistants assigned.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(assistantId))
        {
            TempData["StatusMessage"] = "Please select an assistant.";
            return RedirectToPage();
        }

        var chosen = availableAssistants.FirstOrDefault(a => a.Id == assistantId);
        if (chosen == null)
        {
            TempData["StatusMessage"] = "Invalid assistant selection.";
            return RedirectToPage();
        }

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == doctorProfile.Id);

        if (req == null) return NotFound();

        if (req.Status != PatientMessageRequestStatus.WaitingDoctorApproval)
            return RedirectToPage();

        req.AssistantId = chosen.Id;
        req.Status = PatientMessageRequestStatus.Pending;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _notifier.NotifyAsync(
            chosen,
            NotificationType.System,
            "New Delegated Request",
            $"Dr. {doctorUser.FullName ?? doctorUser.Email} delegated a patient request: {req.Subject}",
            actionUrl: "/Assistant/Messages/Requests/Index",
            actionText: "View Request",
            relatedEntity: "PatientMessageRequest",
            relatedEntityId: req.Id.ToString()
        );

        TempData["StatusMessage"] = "Request delegated.";
        return RedirectToPage();
    }
}
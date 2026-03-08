using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Messages.Requests;

[Authorize(Roles = "Assistant")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifier;

    public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
    {
        _db = db;
        _userManager = userManager;
        _notifier = notifier;
    }

    public IList<PatientMessageRequest> Requests { get; set; } = new List<PatientMessageRequest>();
    public string CurrentAssistantId { get; set; } = "";

    public async Task OnGetAsync(string? status)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return;

        CurrentAssistantId = user.Id;

        var assignedDoctorProfileId = user.AssignedDoctorId;
        if (assignedDoctorProfileId == null)
        {
            Requests = new List<PatientMessageRequest>();
            return;
        }

        var query = _db.PatientMessageRequests
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .Include(r => r.Assistant)
            .Include(r => r.DoctorProfile).ThenInclude(d => d.User)
            .Where(r =>
                r.DoctorProfileId == assignedDoctorProfileId.Value &&
                (
                    (r.Status == PatientMessageRequestStatus.Pending && (r.AssistantId == null || r.AssistantId == user.Id)) ||
                    (r.AssistantId == user.Id)
                ))
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<PatientMessageRequestStatus>(status, out var s))
        {
            query = query.Where(r => r.Status == s);
        }

        Requests = await query
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var assignedDoctorProfileId = user.AssignedDoctorId;
        if (assignedDoctorProfileId == null) return BadRequest();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .Include(r => r.DoctorProfile)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == assignedDoctorProfileId.Value);

        if (req == null) return NotFound();
        if (req.Status != PatientMessageRequestStatus.Pending) return BadRequest();

        if (!string.IsNullOrWhiteSpace(req.AssistantId) && req.AssistantId != user.Id)
        {
            TempData["StatusMessage"] = "This request is assigned to another assistant.";
            return RedirectToPage("/Assistant/Messages/Requests/Index");
        }

        req.AssistantId = user.Id;
        req.Status = PatientMessageRequestStatus.AssistantChat;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var patientUserId = req.Patient.UserId;
        await _notifier.NotifyUserAsync(
            patientUserId,
            "Assistant accepted your request",
            $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant");

        var doctorUserId = req.DoctorProfile.UserId;
        await _notifier.NotifyUserAsync(
            doctorUserId,
            $"Assistant started triage: {req.Subject}",
            "/Doctor/Messages/Requests");

        return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
    }
}
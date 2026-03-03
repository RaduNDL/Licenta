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
public class ReviewModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifier;

    public ReviewModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
    {
        _db = db;
        _userManager = userManager;
        _notifier = notifier;
    }

    public PatientMessageRequest RequestData { get; set; } = null!;
    public bool CanAccept { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.AssignedDoctorId == null) return Unauthorized();

        RequestData = await _db.PatientMessageRequests
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .Include(r => r.DoctorProfile).ThenInclude(d => d.User)
            .Include(r => r.Assistant)
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                r.DoctorProfileId == user.AssignedDoctorId.Value &&
                (r.AssistantId == null || r.AssistantId == user.Id));

        if (RequestData == null) return NotFound();

        CanAccept = RequestData.Status == PatientMessageRequestStatus.Pending && string.IsNullOrEmpty(RequestData.AssistantId);

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        if (user.AssignedDoctorId == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == user.AssignedDoctorId.Value);

        if (req == null) return NotFound();
        if (req.Status != PatientMessageRequestStatus.Pending) return BadRequest();

        if (!string.IsNullOrWhiteSpace(req.AssistantId) && req.AssistantId != user.Id)
        {
            TempData["StatusMessage"] = "This request is assigned to another assistant.";
            return RedirectToPage("./Review", new { id });
        }

        req.AssistantId = user.Id;
        req.Status = PatientMessageRequestStatus.AssistantChat;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _notifier.NotifyUserAsync(
            req.Patient.UserId,
            "Assistant accepted your request",
            $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant");

        return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
    }

    public async Task<IActionResult> OnPostReturnToDoctorAsync(Guid id, string? note)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        if (user.AssignedDoctorId == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .Include(r => r.DoctorProfile)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == user.AssignedDoctorId.Value);

        if (req == null) return NotFound();

        req.AssistantNote = string.IsNullOrWhiteSpace(note) ? "Assistant requested doctor review." : note.Trim();
        req.AssistantId = null;
        req.Status = PatientMessageRequestStatus.WaitingDoctorApproval;
        req.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifier.NotifyUserAsync(
            req.DoctorProfile.UserId,
            $"Request returned by assistant: {req.Subject}",
            "/Doctor/Messages/Requests");

        await _notifier.NotifyUserAsync(
            req.Patient.UserId,
            "Your request is back to the doctor for approval",
            "/Patient/Messages/RequestList");

        TempData["StatusMessage"] = "Request sent back to the doctor.";
        return RedirectToPage("/Assistant/Messages/Requests/Index");
    }
}
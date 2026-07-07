using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Messages.Requests;

[Authorize(Roles = "Assistant")]
[AutoValidateAntiforgeryToken]
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

    public PatientMessageRequest? RequestData { get; set; }
    public bool CanAccept { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var assistantUser = await _db.Users
            .Include(u => u.AssignedDoctors)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        var doctorIds = assistantUser?.AssignedDoctors.Select(d => d.Id).ToList() ?? new List<Guid>();
        if (!doctorIds.Any()) return Unauthorized();

        RequestData = await _db.PatientMessageRequests
            .Include(r => r.Patient!).ThenInclude(p => p.User)
            .Include(r => r.DoctorProfile!).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(r => r.Id == id && doctorIds.Contains(r.DoctorProfileId));

        if (RequestData == null) return NotFound();

        CanAccept = RequestData.Status == PatientMessageRequestStatus.Pending &&
                    (string.IsNullOrEmpty(RequestData.AssistantId) || RequestData.AssistantId == user.Id);

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient!)
            .Include(r => r.DoctorProfile!)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null) return NotFound();

        var assistantUser = await _db.Users
            .Include(u => u.AssignedDoctors)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (assistantUser == null || !assistantUser.AssignedDoctors.Any(d => d.Id == req.DoctorProfileId))
            return Forbid();

        req.AssistantId = user.Id;
        req.Status = PatientMessageRequestStatus.AssistantChat;
        req.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifier.NotifyUserAsync(
            req.Patient!.UserId,
            "Assistant accepted your request",
            $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant");

        await _notifier.NotifyUserAsync(
            req.DoctorProfile!.UserId,
            $"Assistant started triage: {req.Subject}",
            "/Doctor/Messages/Requests");

        return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
    }

    public async Task<IActionResult> OnPostReturnToDoctorAsync(Guid id, string? note)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient!)
            .Include(r => r.DoctorProfile!)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null) return NotFound();

        var assistantUser = await _db.Users
            .Include(u => u.AssignedDoctors)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (assistantUser == null || !assistantUser.AssignedDoctors.Any(d => d.Id == req.DoctorProfileId))
            return Forbid();

        req.AssistantNote = string.IsNullOrWhiteSpace(note)
            ? "Assistant requested doctor review."
            : note.Trim();

        req.AssistantId = user.Id;
        req.Status = PatientMessageRequestStatus.RejectedByAssistant;
        req.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifier.NotifyUserAsync(
            req.DoctorProfile!.UserId,
            $"Request returned: {req.Subject}",
            "/Doctor/Messages/Requests");

        await _notifier.NotifyUserAsync(
            req.Patient!.UserId,
            "Your request is back to the doctor for review",
            "/Patient/Messages/RequestList");

        TempData["StatusMessage"] = "Request sent back to the doctor and marked as Rejected by Assistant.";

        return RedirectToPage("/Assistant/Messages/Requests/Index", new { status = "RejectedByAssistant" });
    }
}
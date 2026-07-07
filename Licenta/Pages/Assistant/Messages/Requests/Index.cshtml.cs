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

        var assistantUser = await _db.Users.Include(u => u.AssignedDoctors).FirstOrDefaultAsync(u => u.Id == user.Id);
        var doctorIds = assistantUser?.AssignedDoctors.Select(d => d.Id).ToList() ?? new List<Guid>();

        if (!doctorIds.Any()) return;

        var query = _db.PatientMessageRequests
            .Include(r => r.Patient!).ThenInclude(p => p.User)
            .Include(r => r.DoctorProfile!).ThenInclude(d => d.User)
            .Where(r => doctorIds.Contains(r.DoctorProfileId) &&
                ((r.Status == PatientMessageRequestStatus.Pending && (r.AssistantId == null || r.AssistantId == user.Id)) ||
                 (r.AssistantId == user.Id)))
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<PatientMessageRequestStatus>(status, out var s))
            query = query.Where(r => r.Status == s);

        Requests = await query.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt).ToListAsync();
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient!).Include(r => r.DoctorProfile!)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null) return NotFound();

        var assistantUser = await _db.Users.Include(u => u.AssignedDoctors).FirstOrDefaultAsync(u => u.Id == user.Id);
        if (assistantUser == null || !assistantUser.AssignedDoctors.Any(d => d.Id == req.DoctorProfileId))
            return Forbid();

        req.AssistantId = user.Id;
        req.Status = PatientMessageRequestStatus.AssistantChat;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _notifier.NotifyUserAsync(req.Patient!.UserId, "Assistant accepted your request", $"/Patient/Messages/Inbox?requestId={req.Id}&kind=Assistant");

        return RedirectToPage("/Assistant/Messages/Inbox", new { requestId = req.Id });
    }
}
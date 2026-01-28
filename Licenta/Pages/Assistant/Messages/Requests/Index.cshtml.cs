using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Messages.Requests
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IList<PatientMessageRequest> Requests { get; set; } = new List<PatientMessageRequest>();
        public string? StatusFilter { get; set; }
        public string CurrentAssistantId { get; set; } = string.Empty;

        public async Task OnGetAsync(string? status)
        {
            StatusFilter = status;

            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return;

            CurrentAssistantId = assistant.Id;
            var clinicId = (assistant.ClinicId ?? "").Trim();

            var q = _db.PatientMessageRequests
                .AsNoTracking()
                .Include(r => r.Patient)
                .Include(r => r.Assistant)
                .Include(r => r.Doctor)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(clinicId))
                q = q.Where(r => (r.Patient!.ClinicId ?? "").Trim() == clinicId);

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<PatientMessageRequestStatus>(status, out var st))
            {
                q = q.Where(r => r.Status == st);
            }

            q = q.Where(r =>
                r.Status == PatientMessageRequestStatus.Pending ||
                r.AssistantId == assistant.Id);

            Requests = await q
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAcceptAsync(Guid id)
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return Unauthorized();

            var clinicId = (assistant.ClinicId ?? "").Trim();

            await using var tx = await _db.Database.BeginTransactionAsync();

            var req = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                var patientClinicId = (req.Patient?.ClinicId ?? "").Trim();
                if (patientClinicId != clinicId) return Forbid();
            }

            if (req.Status != PatientMessageRequestStatus.Pending)
            {
                TempData["StatusMessage"] = "Request is not pending.";
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(req.AssistantId) &&
                req.AssistantId != assistant.Id)
            {
                TempData["StatusMessage"] = "This request was already accepted by another assistant.";
                return RedirectToPage();
            }

            req.AssistantId = assistant.Id;
            req.Status = PatientMessageRequestStatus.AssistantChat;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["StatusMessage"] = "Request accepted. Chat opened.";

            return RedirectToPage("/Assistant/Messages/Inbox",
                new { requestId = req.Id });
        }
    }
}

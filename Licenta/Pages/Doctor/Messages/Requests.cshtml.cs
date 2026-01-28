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

namespace Licenta.Pages.Doctor.Messages
{
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

        public record RequestVm(Guid Id, string PatientName, string Subject, string EscalationReason, string CreatedAtLocal);

        public List<RequestVm> Requests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null) return;

            Requests = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Where(r =>
                    r.DoctorId == doctor.Id &&
                    r.Status == PatientMessageRequestStatus.WaitingDoctorApproval)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RequestVm(
                    r.Id,
                    r.Patient.FullName ?? r.Patient.Email ?? "Patient",
                    r.Subject,
                    r.EscalationReason ?? "-",
                    r.CreatedAt.ToLocalTime().ToString("g")
                ))
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAcceptAsync(Guid id)
        {
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null) return Unauthorized();

            var req = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Assistant)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null || req.DoctorId != doctor.Id)
                return Forbid();

            req.Status = PatientMessageRequestStatus.ActiveDoctorChat;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (req.Patient != null)
            {
                await _notifier.NotifyAsync(
                    req.Patient,
                    NotificationType.Message,
                    "Doctor accepted your case",
                    "You can now chat with the doctor.",
                    "PatientMessageRequest",
                    req.Id.ToString(),
                    false);
            }

            TempData["StatusMessage"] = "Request accepted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeclineAsync(Guid id)
        {
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null) return Unauthorized();

            var req = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Assistant)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null || req.DoctorId != doctor.Id)
                return Forbid();

            req.Status = PatientMessageRequestStatus.RejectedByDoctor;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Request declined.";
            return RedirectToPage();
        }
    }
}

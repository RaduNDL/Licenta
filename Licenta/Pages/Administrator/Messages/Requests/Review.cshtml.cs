using System;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Administrator.Messages.Requests
{
    [Authorize(Roles = "Administrator")]
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public PatientMessageRequest RequestItem { get; set; } = default!;

        [BindProperty]
        public string? AdminNote { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var request = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            RequestItem = request;
            AdminNote = request.AdminNote;
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid id)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
                return Forbid();

            var request = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            if (request.Status != PatientMessageRequestStatus.Pending)
                return RedirectToPage("Index");

            if (string.IsNullOrWhiteSpace(request.PatientId) || string.IsNullOrWhiteSpace(request.DoctorId))
            {
                TempData["StatusMessage"] = "Invalid request: missing patient or doctor id.";
                return RedirectToPage("Index");
            }

            request.Status = PatientMessageRequestStatus.Approved;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = admin.Id;
            request.AdminNote = AdminNote;

            var message = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = request.PatientId!,      
                RecipientId = request.DoctorId!,    
                Subject = request.Subject ?? string.Empty,
                Body = request.Body ?? string.Empty,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.InternalMessages.Add(message);

            var doctorDisplay = request.Doctor?.FullName
                                ?? request.Doctor?.Email
                                ?? "the doctor";

            var notifyPatient = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = admin.Id,
                RecipientId = request.PatientId!,   // safe
                Subject = "Your message to the doctor was approved",
                Body = $"Your request to message {doctorDisplay} was approved by an administrator.",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.InternalMessages.Add(notifyPatient);

            await _db.SaveChangesAsync();
            return RedirectToPage("Index", new { status = "Pending" });
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid id)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
                return Forbid();

            var request = await _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            if (request.Status != PatientMessageRequestStatus.Pending)
                return RedirectToPage("Index");

            if (string.IsNullOrWhiteSpace(request.PatientId))
            {
                TempData["StatusMessage"] = "Invalid request: missing patient id.";
                return RedirectToPage("Index");
            }

            request.Status = PatientMessageRequestStatus.Rejected;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = admin.Id;
            request.AdminNote = AdminNote;

            var body = AdminNote ?? "Your request to contact the doctor was rejected by an administrator.";
            var notifyPatient = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = admin.Id,
                RecipientId = request.PatientId!,   
                Subject = "Your message request was rejected",
                Body = body,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.InternalMessages.Add(notifyPatient);

            await _db.SaveChangesAsync();
            return RedirectToPage("Index", new { status = "Pending" });
        }
    }
}

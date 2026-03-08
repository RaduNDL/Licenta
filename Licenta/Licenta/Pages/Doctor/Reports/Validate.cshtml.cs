using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Reports
{
    [Authorize(Roles = "Doctor")]
    public class ValidateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifier;

        public ValidateModel(AppDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _notifier = notifier;
        }

        public List<MedicalRecord> Pending { get; set; } = new();

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["StatusMessage"] = "User not found.";
                Pending = new();
                return;
            }

            if (currentUser.DoctorProfile == null)
            {
                currentUser = await _db.Users
                    .Include(u => u.DoctorProfile)
                    .FirstOrDefaultAsync(u => u.Id == currentUser.Id);
            }

            var doctorId = currentUser?.DoctorProfile?.Id;
            if (doctorId == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                Pending = new();
                return;
            }

            Pending = await _db.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Where(r => r.DoctorId == doctorId.Value && r.Status == RecordStatus.Draft)
                .OrderByDescending(r => r.VisitDateUtc)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync(Guid recordId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage();
            }

            if (currentUser.DoctorProfile == null)
            {
                currentUser = await _db.Users
                    .Include(u => u.DoctorProfile)
                    .FirstOrDefaultAsync(u => u.Id == currentUser.Id);
            }

            var doctorId = currentUser?.DoctorProfile?.Id;
            if (doctorId == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                return RedirectToPage();
            }

            var record = await _db.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == recordId && r.DoctorId == doctorId.Value);

            if (record == null)
            {
                TempData["StatusMessage"] = "Error: record not found.";
                return RedirectToPage();
            }

            record.Status = RecordStatus.Validated;
            record.ValidatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var patientUser = record.Patient?.User;
            if (patientUser != null)
            {
                await _notifier.NotifyAsync(
                    patientUser,
                    NotificationType.Document,
                    "Medical Report Validated",
                    $"Dr. {currentUser.FullName ?? currentUser.Email} has validated a new medical report for your recent visit.",
                    actionUrl: $"/Patient/MedicalRecords/Details?id={record.Id}",
                    actionText: "View Report",
                    relatedEntity: "MedicalRecord",
                    relatedEntityId: record.Id.ToString(),
                    sendEmail: false
                );
            }

            var patientName = patientUser?.FullName ?? "patient";
            TempData["StatusMessage"] = $"Record for {patientName} has been validated.";
            return RedirectToPage();
        }
    }
}
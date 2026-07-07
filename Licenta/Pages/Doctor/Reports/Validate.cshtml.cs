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
        public List<MedicalAttachment> PendingAttachments { get; set; } = new();
        public Guid CurrentDoctorId { get; set; }

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["StatusMessage"] = "User not found.";
                Pending = new();
                PendingAttachments = new();
                return;
            }

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                Pending = new();
                PendingAttachments = new();
                return;
            }

            CurrentDoctorId = doctor.Id;

            Pending = await _db.MedicalRecords
                .AsNoTracking()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Where(r => r.DoctorId == doctor.Id && r.Status == RecordStatus.Draft)
                .OrderByDescending(r => r.VisitDateUtc)
                .ToListAsync();

            PendingAttachments = await LoadPendingAttachmentsAsync(currentUser, doctor.Id);
        }

        public async Task<IActionResult> OnPostAsync(Guid recordId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage();
            }

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                return RedirectToPage();
            }

            var record = await _db.MedicalRecords
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == recordId && r.DoctorId == doctor.Id);

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

        public async Task<IActionResult> OnPostClaimAttachmentAsync(Guid attachmentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["StatusMessage"] = "User not found.";
                return RedirectToPage();
            }

            var doctor = await _db.Doctors
                .FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

            if (doctor == null)
            {
                TempData["StatusMessage"] = "Doctor profile not found.";
                return RedirectToPage();
            }

            var attachment = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a =>
                    a.Id == attachmentId &&
                    a.Status == AttachmentStatus.Pending &&
                    a.DoctorId == null &&
                    a.Type != "ProfilePhoto" &&
                    a.Type != "AppointmentRequest");

            if (attachment == null)
            {
                TempData["StatusMessage"] = "Attachment not found or already assigned.";
                return RedirectToPage();
            }

            var doctorClinicId = (currentUser.ClinicId ?? "").Trim();
            var patientClinicId = (attachment.Patient?.User?.ClinicId ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(doctorClinicId) && patientClinicId != doctorClinicId)
            {
                TempData["StatusMessage"] = "You cannot claim an attachment from another clinic.";
                return RedirectToPage();
            }

            attachment.DoctorId = doctor.Id;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Attachment assigned to you.";
            return RedirectToPage("/Doctor/Attachments/Review", new { id = attachment.Id });
        }

        private async Task<List<MedicalAttachment>> LoadPendingAttachmentsAsync(ApplicationUser currentUser, Guid doctorId)
        {
            var clinicId = (currentUser.ClinicId ?? "").Trim();

            var query = _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Where(a =>
                    a.Status == AttachmentStatus.Pending &&
                    a.Type != "ProfilePhoto" &&
                    a.Type != "AppointmentRequest" &&
                    (a.DoctorId == null || a.DoctorId == doctorId));

            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                query = query.Where(a =>
                    a.Patient != null &&
                    a.Patient.User != null &&
                    (a.Patient.User.ClinicId ?? "").Trim() == clinicId);
            }

            return await query
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }
    }
}

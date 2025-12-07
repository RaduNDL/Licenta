using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class AppointmentRequestsPageModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;

        public AppointmentRequestsPageModel(AppDbContext db, INotificationService notifier, IWebHostEnvironment env)
        {
            _db = db;
            _notifier = notifier;
            _env = env;
        }

        public class RequestRowVm
        {
            public Guid AttachmentId { get; set; }
            public string PatientName { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public DateTime UploadedAt { get; set; }
            public DateTime? PreferredDate { get; set; }
            public string? PreferredTime { get; set; }
            public string? Reason { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? SuggestedScheduledLocal { get; set; }
        }

        private class AppointmentRequestPayload
        {
            public DateTime PreferredDate { get; set; }
            public string? PreferredTime { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public List<RequestRowVm> PendingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var attachments = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.Type == "AppointmentRequest" && a.Status == AttachmentStatus.Pending)
                .OrderBy(a => a.UploadedAt)
                .ToListAsync();

            var rows = new List<RequestRowVm>();

            foreach (var att in attachments)
            {
                DateTime? preferredDate = null;
                string? preferredTime = null;
                string? reason = null;
                string? suggested = null;

                try
                {
                    var relative = att.FilePath?.TrimStart('/') ?? string.Empty;
                    var physicalPath = Path.Combine(_env.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(physicalPath))
                    {
                        var json = await System.IO.File.ReadAllTextAsync(physicalPath);
                        var payload = JsonSerializer.Deserialize<AppointmentRequestPayload>(json);
                        if (payload != null)
                        {
                            preferredDate = payload.PreferredDate;
                            preferredTime = payload.PreferredTime;
                            reason = payload.Reason;

                            if (payload.PreferredTime != null && TimeSpan.TryParse(payload.PreferredTime, out var ts))
                            {
                                var dateOnly = payload.PreferredDate.Date;
                                var localDt = DateTime.SpecifyKind(dateOnly + ts, DateTimeKind.Local);
                                suggested = localDt.ToString("yyyy-MM-ddTHH:mm");
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore malformed JSON or missing files
                }

                rows.Add(new RequestRowVm
                {
                    AttachmentId = att.Id,
                    PatientName = att.Patient?.User?.FullName ?? att.Patient?.User?.Email ?? "Unknown patient",
                    DoctorName = att.Doctor?.User?.FullName ?? att.Doctor?.User?.Email ?? "Unknown doctor",
                    UploadedAt = att.UploadedAt,
                    PreferredDate = preferredDate,
                    PreferredTime = preferredTime,
                    Reason = reason,
                    Status = att.Status.ToString(), // enum to string
                    SuggestedScheduledLocal = suggested
                });
            }

            PendingRequests = rows;
        }

        public async Task<IActionResult> OnPostAcceptAsync(Guid attachmentId, DateTime scheduledLocal)
        {
            if (attachmentId == Guid.Empty)
                return RedirectToPage();

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.Type == "AppointmentRequest");

            if (att == null)
                return NotFound();

            if (att.Status != AttachmentStatus.Pending)
            {
                TempData["StatusMessage"] = "This request has already been processed.";
                return RedirectToPage();
            }

            AppointmentRequestPayload? payload = null;
            try
            {
                var relative = att.FilePath?.TrimStart('/') ?? string.Empty;
                var physicalPath = Path.Combine(_env.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physicalPath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(physicalPath);
                    payload = JsonSerializer.Deserialize<AppointmentRequestPayload>(json);
                }
            }
            catch
            {
            }

            var appointment = new Appointment
            {
                PatientId = att.PatientId,
                DoctorId = att.DoctorId,
                ScheduledAt = DateTime.SpecifyKind(scheduledLocal, DateTimeKind.Local).ToUniversalTime(),
                Reason = payload?.Reason ?? "Appointment approved from patient request.",
                Status = AppointmentStatus.Approved,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);

            att.Status = AttachmentStatus.Validated;
            att.ValidatedAtUtc = DateTime.UtcNow;
            att.ValidationNotes = $"Approved for {scheduledLocal:f}";

            await _db.SaveChangesAsync();

            var patientUser = att.Patient?.User;
            var doctorUser = att.Doctor?.User;

            if (patientUser != null)
            {
                await _notifier.NotifyAsync(patientUser, "Appointment request approved",
                    $"Your appointment request has been approved.<br/>Scheduled for: {appointment.ScheduledAt.ToLocalTime():f}.");
            }

            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(doctorUser, "New appointment from request",
                    $"New appointment from patient {patientUser?.FullName ?? patientUser?.Email}<br/>When: {appointment.ScheduledAt.ToLocalTime():f}<br/>Reason: {appointment.Reason}");
            }

            TempData["StatusMessage"] = "Request approved and appointment created.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid attachmentId, string? reason)
        {
            if (attachmentId == Guid.Empty)
                return RedirectToPage();

            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.Type == "AppointmentRequest");

            if (att == null)
                return NotFound();

            if (att.Status != AttachmentStatus.Pending)
            {
                TempData["StatusMessage"] = "This request has already been processed.";
                return RedirectToPage();
            }

            att.Status = AttachmentStatus.Rejected;
            att.ValidationNotes = reason;
            att.ValidatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var patientUser = att.Patient?.User;
            var doctorUser = att.Doctor?.User;

            if (patientUser != null)
            {
                await _notifier.NotifyAsync(patientUser, "Appointment request rejected",
                    $"Your appointment request was rejected.<br/>Reason: {reason}");
            }

            if (doctorUser != null)
            {
                await _notifier.NotifyAsync(doctorUser, "Appointment request rejected",
                    $"Request from patient {patientUser?.FullName ?? patientUser?.Email} was rejected.<br/>Reason: {reason}");
            }

            TempData["StatusMessage"] = "Request rejected.";
            return RedirectToPage();
        }
    }
}

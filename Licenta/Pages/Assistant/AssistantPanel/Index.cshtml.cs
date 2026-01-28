using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.AssistantPanel
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public IndexModel(AppDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        public string AssistantName { get; set; } = "Assistant";

        public int PendingAppointmentRequestsCount { get; set; }
        public int UnreadMessagesCount { get; set; }
        public int PendingRescheduleRequestsCount { get; set; }
        public int TotalPatients { get; set; }

        public List<AppointmentRequestVm> RecentAppointmentRequests { get; set; } = new();
        public List<NotificationVm> RecentNotifications { get; set; } = new();
        public List<UpcomingAppointmentVm> UpcomingAppointments { get; set; } = new();

        public class AppointmentRequestVm
        {
            public Guid AttachmentId { get; set; }
            public string PatientName { get; set; } = "";
            public string? DoctorName { get; set; }
            public string Reason { get; set; } = "-";
            public string PreferredDisplay { get; set; } = "-";
            public bool IsEscalatedToDoctor { get; set; }
        }

        public class NotificationVm
        {
            public string Type { get; set; } = "";
            public string Message { get; set; } = "";
            public string TimeAgo { get; set; } = "";
        }

        public class UpcomingAppointmentVm
        {
            public string PatientName { get; set; } = "";
            public string DoctorName { get; set; } = "";
            public string WhenLocal { get; set; } = "";
            public string Status { get; set; } = "";
        }

        private class AppointmentRequestPayload
        {
            public string SelectedLocalIso { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return;

            AssistantName = assistant.FullName ?? assistant.Email ?? "Assistant";
            var clinicId = assistant.ClinicId;

            TotalPatients = await _context.Patients
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => string.IsNullOrWhiteSpace(clinicId) || (p.User != null && p.User.ClinicId == clinicId))
                .CountAsync();

            var apptReqQuery = _context.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.Type == "AppointmentRequest" && a.Status == AttachmentStatus.Pending);

            if (!string.IsNullOrWhiteSpace(clinicId))
                apptReqQuery = apptReqQuery.Where(a => a.Patient != null && a.Patient.User != null && a.Patient.User.ClinicId == clinicId);

            PendingAppointmentRequestsCount = await apptReqQuery.CountAsync();

            var apptReqItems = await apptReqQuery
                .OrderByDescending(a => a.UploadedAt)
                .Take(10)
                .ToListAsync();

            RecentAppointmentRequests = new();

            foreach (var a in apptReqItems)
            {
                var payload = await TryReadAppointmentPayloadAsync(a.FilePath);

                var isEscalated =
                    !string.IsNullOrWhiteSpace(a.ValidationNotes) &&
                    a.ValidationNotes.StartsWith("AWAITING_DOCTOR_APPROVAL", StringComparison.OrdinalIgnoreCase);

                var preferred = "-";

                if (isEscalated && !string.IsNullOrWhiteSpace(a.ValidationNotes))
                {
                    var idx = a.ValidationNotes.IndexOf("Suggested:", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                        preferred = a.ValidationNotes[(idx + "Suggested:".Length)..].Trim();
                }
                else if (!string.IsNullOrWhiteSpace(payload?.SelectedLocalIso))
                {
                    preferred = FormatLocalIsoForDisplay(payload!.SelectedLocalIso);
                }

                var reason = !string.IsNullOrWhiteSpace(payload?.Reason)
                    ? payload!.Reason
                    : (string.IsNullOrWhiteSpace(a.PatientNotes) ? "-" : a.PatientNotes!);

                RecentAppointmentRequests.Add(new AppointmentRequestVm
                {
                    AttachmentId = a.Id,
                    PatientName = a.Patient?.User?.FullName ?? a.Patient?.User?.Email ?? "Unknown",
                    DoctorName = a.Doctor?.User?.FullName ?? a.Doctor?.User?.Email,
                    Reason = reason,
                    PreferredDisplay = preferred,
                    IsEscalatedToDoctor = isEscalated
                });
            }

            UnreadMessagesCount = await _context.InternalMessages
                .AsNoTracking()
                .Where(m => m.RecipientId == assistant.Id && !m.IsRead)
                .CountAsync();

            PendingRescheduleRequestsCount = await _context.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Where(r =>
                    r.Status == AppointmentRescheduleStatus.Requested ||
                    r.Status == AppointmentRescheduleStatus.Proposed ||
                    r.Status == AppointmentRescheduleStatus.PatientSelected)
                .CountAsync();

            var nowUtc = DateTime.UtcNow;

            var notes = await _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == assistant.Id)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(12)
                .ToListAsync();

            RecentNotifications = notes
                .Select(n => new NotificationVm
                {
                    Type = n.Type.ToString(),
                    Message = n.Message ?? "",
                    TimeAgo = FormatAgo(n.CreatedAtUtc, nowUtc)
                })
                .ToList();

            var apptQuery = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.StartTimeUtc >= nowUtc);

            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                apptQuery = apptQuery.Where(a =>
                    a.Patient != null &&
                    a.Patient.User != null &&
                    a.Patient.User.ClinicId == clinicId);
            }

            var appts = await apptQuery
                .OrderBy(a => a.StartTimeUtc)
                .Take(12)
                .ToListAsync();

            UpcomingAppointments = appts.Select(a => new UpcomingAppointmentVm
            {
                PatientName = a.Patient?.User?.FullName ?? a.Patient?.User?.Email ?? "Patient",
                DoctorName = a.Doctor?.User?.FullName ?? a.Doctor?.User?.Email ?? "Doctor",
                WhenLocal = a.StartTimeUtc.ToLocalTime().ToString("ddd dd MMM HH:mm"),
                Status = a.Status.ToString()
            }).ToList(); 
        }

        private async Task<AppointmentRequestPayload?> TryReadAppointmentPayloadAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            try
            {
                var relative = filePath.TrimStart('/');
                var physicalPath = Path.Combine(_env.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(physicalPath))
                    return null;

                var json = await System.IO.File.ReadAllTextAsync(physicalPath);
                return JsonSerializer.Deserialize<AppointmentRequestPayload>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatLocalIsoForDisplay(string localIso)
        {
            if (!DateTime.TryParseExact(localIso, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return localIso;

            var local = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return local.ToString("ddd dd MMM HH:mm");
        }

        private static string FormatAgo(DateTime createdAtUtc, DateTime nowUtc)
        {
            var diff = nowUtc - createdAtUtc;

            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hrs ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";

            return createdAtUtc.ToLocalTime().ToString("g");
        }
    }
}

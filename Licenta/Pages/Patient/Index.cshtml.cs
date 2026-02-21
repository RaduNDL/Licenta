using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Patient
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public string PatientName { get; set; } = string.Empty;

        public int UpcomingAppointmentsCount { get; set; }
        public int NewResultsCount { get; set; }
        public int UnreadMessagesCount { get; set; }
        public int ActiveRequestsCount { get; set; }
        public int ActiveRescheduleRequestsCount { get; set; }

        public NextAppointmentViewModel? NextAppointment { get; set; }

        public List<ActivityViewModel> RecentActivities { get; set; } = new();
        public List<NotificationViewModel> RecentNotifications { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            PatientName = user.FullName ?? user.Email ?? "Patient";

            var patientProfile = await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patientProfile == null)
            {
                UpcomingAppointmentsCount = 0;
                NewResultsCount = 0;
                UnreadMessagesCount = 0;
                ActiveRequestsCount = 0;
                ActiveRescheduleRequestsCount = 0;
                NextAppointment = null;
                RecentActivities = new();
                RecentNotifications = new();
                return;
            }

            var nowUtc = DateTime.UtcNow;

            UpcomingAppointmentsCount = await _context.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.PatientId == patientProfile.Id &&
                    a.ScheduledAt >= nowUtc &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.NoShow)
                .CountAsync();

            var resultsSinceUtc = nowUtc.AddDays(-7);

            NewResultsCount = await _context.MedicalAttachments
                .AsNoTracking()
                .Where(a =>
                    a.PatientId == patientProfile.Id &&
                    a.Status == AttachmentStatus.Validated &&
                    a.ValidatedAtUtc != null &&
                    a.ValidatedAtUtc >= resultsSinceUtc)
                .CountAsync();

            UnreadMessagesCount = await _context.InternalMessages
                .AsNoTracking()
                .Where(m => m.RecipientId == user.Id && !m.IsRead)
                .CountAsync();

            ActiveRequestsCount = await _context.PatientMessageRequests
                .AsNoTracking()
                .Where(r =>
                    r.PatientId == user.Id &&
                    r.Status != PatientMessageRequestStatus.Closed &&
                    r.Status != PatientMessageRequestStatus.RejectedByDoctor)
                .CountAsync();

            ActiveRescheduleRequestsCount = await _context.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Where(r =>
                    r.PatientId == patientProfile.Id &&
                    (r.Status == AppointmentRescheduleStatus.Requested ||
                     r.Status == AppointmentRescheduleStatus.Proposed ||
                     r.Status == AppointmentRescheduleStatus.PatientSelected))
                .CountAsync();

            var nextAppt = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a =>
                    a.PatientId == patientProfile.Id &&
                    a.ScheduledAt >= nowUtc &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.NoShow)
                .OrderBy(a => a.ScheduledAt)
                .FirstOrDefaultAsync();

            if (nextAppt != null)
            {
                var doctorName = nextAppt.Doctor?.User?.FullName;
                if (string.IsNullOrWhiteSpace(doctorName))
                    doctorName = nextAppt.Doctor?.User?.Email ?? "Doctor";

                var canReschedule = nextAppt.Status != AppointmentStatus.Cancelled
                                    && nextAppt.Status != AppointmentStatus.Completed
                                    && nextAppt.ScheduledAt > DateTime.UtcNow;

                NextAppointment = new NextAppointmentViewModel
                {
                    AppointmentId = nextAppt.Id,
                    Date = nextAppt.ScheduledAt.ToLocalTime(),
                    Time = nextAppt.ScheduledAt.ToLocalTime().ToString("HH:mm"),
                    DoctorName = doctorName,
                    CanReschedule = canReschedule
                };
            }
            else
            {
                NextAppointment = null;
            }

            RecentActivities = await BuildRecentActivitiesAsync(
                patientId: patientProfile.Id,
                userId: user.Id,
                nowUtc: nowUtc,
                sinceUtc: nowUtc.AddDays(-7),
                take: 6
            );

            RecentNotifications = await _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(6)
                .Select(n => new NotificationViewModel
                {
                    Title = string.IsNullOrWhiteSpace(n.Title) ? "Notification" : n.Title,
                    Message = SanitizeNotificationText(n.Message),
                    TimeAgo = FormatAgo(n.CreatedAtUtc, nowUtc)
                })
                .ToListAsync();
        }

        private async Task<List<ActivityViewModel>> BuildRecentActivitiesAsync(Guid patientId, string userId, DateTime nowUtc, DateTime sinceUtc, int take)
        {
            var items = new List<ActivityItem>();

            var appts = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientId && (a.CreatedAtUtc >= sinceUtc || (a.UpdatedAtUtc != null && a.UpdatedAtUtc >= sinceUtc) || a.ScheduledAt >= sinceUtc))
                .OrderByDescending(a => a.UpdatedAtUtc ?? a.CreatedAtUtc)
                .Take(12)
                .Select(a => new
                {
                    a.Id,
                    a.Status,
                    a.ScheduledAt,
                    a.CreatedAtUtc,
                    a.UpdatedAtUtc,
                    DoctorName = a.Doctor.User.FullName ?? a.Doctor.User.Email ?? "Doctor"
                })
                .ToListAsync();

            foreach (var a in appts)
            {
                var whenUtc = a.UpdatedAtUtc ?? a.CreatedAtUtc;
                var title = a.Status switch
                {
                    AppointmentStatus.Pending => "Appointment requested",
                    AppointmentStatus.Confirmed => "Appointment confirmed",
                    AppointmentStatus.Completed => "Appointment completed",
                    AppointmentStatus.Cancelled => "Appointment cancelled",
                    AppointmentStatus.NoShow => "Appointment marked as no-show",
                    _ => "Appointment updated"
                };

                var desc = $"With {a.DoctorName} on {a.ScheduledAt.ToLocalTime():g}.";
                items.Add(new ActivityItem { OccurredAtUtc = whenUtc, Title = title, Description = desc });
            }

            var reschedules = await _context.Set<AppointmentRescheduleRequest>()
                .AsNoTracking()
                .Where(r => r.PatientId == patientId && (r.CreatedAtUtc >= sinceUtc || r.UpdatedAtUtc >= sinceUtc))
                .OrderByDescending(r => r.UpdatedAtUtc)
                .Take(12)
                .Select(r => new { r.Id, r.Status, r.CreatedAtUtc, r.UpdatedAtUtc })
                .ToListAsync();

            foreach (var r in reschedules)
            {
                var title = r.Status switch
                {
                    AppointmentRescheduleStatus.Requested => "Reschedule requested",
                    AppointmentRescheduleStatus.Proposed => "Reschedule proposed",
                    AppointmentRescheduleStatus.PatientSelected => "Reschedule option selected",
                    AppointmentRescheduleStatus.Approved => "Reschedule approved",
                    AppointmentRescheduleStatus.Rejected => "Reschedule rejected",
                    AppointmentRescheduleStatus.Cancelled => "Reschedule cancelled",
                    _ => "Reschedule updated"
                };
                items.Add(new ActivityItem
                {
                    OccurredAtUtc = r.UpdatedAtUtc,
                    Title = title,
                    Description = $"Request #{r.Id} updated."
                });
            }

            var attachments = await _context.MedicalAttachments
                .AsNoTracking()
                .Where(a => a.PatientId == patientId && (a.UploadedAt >= sinceUtc || (a.ValidatedAtUtc != null && a.ValidatedAtUtc >= sinceUtc)))
                .OrderByDescending(a => a.ValidatedAtUtc ?? a.UploadedAt)
                .Take(12)
                .Select(a => new { a.Id, a.FileName, a.Status, a.UploadedAt, a.ValidatedAtUtc })
                .ToListAsync();

            foreach (var a in attachments)
            {
                items.Add(new ActivityItem
                {
                    OccurredAtUtc = (a.ValidatedAtUtc ?? a.UploadedAt),
                    Title = a.Status == AttachmentStatus.Validated ? "Document validated" : "Document uploaded",
                    Description = a.Status == AttachmentStatus.Validated ? $"{a.FileName} validated." : $"{a.FileName} uploaded."
                });
            }

            var lab = await _context.LabResults
                .AsNoTracking()
                .Where(l => l.PatientId == patientId && (l.UploadedAt >= sinceUtc || (l.ValidatedAtUtc != null && l.ValidatedAtUtc >= sinceUtc)))
                .OrderByDescending(l => l.ValidatedAtUtc ?? l.UploadedAt)
                .Take(12)
                .Select(l => new { l.Id, l.FileName, l.Status, l.UploadedAt, l.ValidatedAtUtc })
                .ToListAsync();

            foreach (var l in lab)
            {
                items.Add(new ActivityItem
                {
                    OccurredAtUtc = (l.ValidatedAtUtc ?? l.UploadedAt),
                    Title = l.ValidatedAtUtc != null ? "Lab result validated" : "Lab result uploaded",
                    Description = $"{l.FileName}"
                });
            }

            var preds = await _context.Predictions
                .AsNoTracking()
                .Where(p => p.PatientId == patientId && (p.CreatedAtUtc >= sinceUtc || (p.ValidatedAtUtc != null && p.ValidatedAtUtc >= sinceUtc)))
                .OrderByDescending(p => p.ValidatedAtUtc ?? p.CreatedAtUtc)
                .Take(12)
                .Select(p => new { p.Id, p.ModelName, p.ResultLabel, p.Status, p.CreatedAtUtc, p.ValidatedAtUtc })
                .ToListAsync();

            foreach (var p in preds)
            {
                var isValidated = p.Status == PredictionStatus.Validated || p.ValidatedAtUtc != null;
                items.Add(new ActivityItem
                {
                    OccurredAtUtc = (p.ValidatedAtUtc ?? p.CreatedAtUtc),
                    Title = isValidated ? "AI result available" : "AI analysis created",
                    Description = isValidated
                        ? $"{p.ModelName}: {(string.IsNullOrWhiteSpace(p.ResultLabel) ? "Result updated" : p.ResultLabel)}."
                        : $"{p.ModelName} created."
                });
            }

            var forms = await _context.MlIntakeForms
                .AsNoTracking()
                .Where(f => f.PatientId == patientId && f.CreatedAt >= sinceUtc)
                .OrderByDescending(f => f.CreatedAt)
                .Take(12)
                .Select(f => new { f.Id, f.CreatedAt })
                .ToListAsync();

            foreach (var f in forms)
            {
                items.Add(new ActivityItem
                {
                    OccurredAtUtc = f.CreatedAt,
                    Title = "Pre-consultation submitted",
                    Description = $"Form #{f.Id} submitted."
                });
            }

            var messages = await _context.InternalMessages
                .AsNoTracking()
                .Where(m => (m.SenderId == userId || m.RecipientId == userId) && m.SentAt >= sinceUtc)
                .OrderByDescending(m => m.SentAt)
                .Take(12)
                .Select(m => new { m.SentAt, m.SenderId, m.RecipientId, m.Subject })
                .ToListAsync();

            foreach (var m in messages)
            {
                var outgoing = m.SenderId == userId;
                items.Add(new ActivityItem
                {
                    OccurredAtUtc = m.SentAt,
                    Title = outgoing ? "Message sent" : "Message received",
                    Description = string.IsNullOrWhiteSpace(m.Subject) ? "Conversation update." : m.Subject
                });
            }

            return items
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(take)
                .Select(x => new ActivityViewModel
                {
                    Title = x.Title,
                    Description = x.Description,
                    TimeAgo = FormatAgo(x.OccurredAtUtc, nowUtc)
                })
                .ToList();
        }

        private static string SanitizeNotificationText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            var decoded = System.Net.WebUtility.HtmlDecode(input);
            decoded = Regex.Replace(decoded, "<.*?>", " ");
            decoded = Regex.Replace(decoded, "\\s+", " ").Trim();
            return decoded;
        }

        private static string FormatAgo(DateTime occurredAtUtc, DateTime nowUtc)
        {
            var diff = nowUtc - occurredAtUtc;

            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hrs ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";

            return occurredAtUtc.ToLocalTime().ToString("g");
        }

        private class ActivityItem
        {
            public DateTime OccurredAtUtc { get; set; }
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public class NextAppointmentViewModel
        {
            public int AppointmentId { get; set; }
            public DateTime Date { get; set; }
            public string Time { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public bool CanReschedule { get; set; }
        }

        public class ActivityViewModel
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string TimeAgo { get; set; } = string.Empty;
        }

        public class NotificationViewModel
        {
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string TimeAgo { get; set; } = string.Empty;
        }
    }
}

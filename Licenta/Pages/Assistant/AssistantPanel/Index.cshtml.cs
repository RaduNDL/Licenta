using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
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

        public IndexModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public string AssistantName { get; set; } = "Assistant";
        public int NewInquiriesCount { get; set; }
        public int ActiveTriageCount { get; set; }
        public int UnreadMessagesCount { get; set; }
        public int EscalatedToDoctorCount { get; set; }

        public List<TriageRequestVm> RecentRequests { get; set; } = new();
        public List<NotificationVm> RecentNotifications { get; set; } = new();

        public class TriageRequestVm
        {
            public Guid RequestId { get; set; }
            public string PatientName { get; set; } = "";
            public string Subject { get; set; } = "";
            public string Status { get; set; } = "";
            public string TimeAgo { get; set; } = "";
        }

        public class NotificationVm
        {
            public string Type { get; set; } = "";
            public string Message { get; set; } = "";
            public string TimeAgo { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null) return;

            AssistantName = assistant.FullName ?? assistant.Email ?? "Assistant";
            var nowUtc = DateTime.UtcNow;

            var baseQuery = _context.PatientMessageRequests
                .AsNoTracking()
                .Where(r => r.AssistantId == assistant.Id);

            NewInquiriesCount = await baseQuery.CountAsync(r => r.Status == PatientMessageRequestStatus.Pending);
            ActiveTriageCount = await baseQuery.CountAsync(r => r.Status == PatientMessageRequestStatus.AssistantChat);
            EscalatedToDoctorCount = await baseQuery.CountAsync(r =>
                r.Status == PatientMessageRequestStatus.WaitingDoctorApproval ||
                r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

            UnreadMessagesCount = await _context.InternalMessages
                .AsNoTracking()
                .Where(m => m.RecipientId == assistant.Id && !m.IsRead)
                .CountAsync();

            RecentRequests = await baseQuery
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Take(6)
                .Select(r => new TriageRequestVm
                {
                    RequestId = r.Id,
                    PatientName = r.Patient.FullName ?? r.Patient.Email ?? "Unknown",
                    Subject = r.Subject ?? "No Subject",
                    Status = r.Status.ToString(),
                    TimeAgo = FormatAgo(r.UpdatedAt ?? r.CreatedAt, nowUtc)
                }).ToListAsync();

            RecentNotifications = await _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == assistant.Id)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(6)
                .Select(n => new NotificationVm
                {
                    Type = n.Type.ToString(),
                    Message = n.Message ?? "",
                    TimeAgo = FormatAgo(n.CreatedAtUtc, nowUtc)
                }).ToListAsync();
        }

        private static string FormatAgo(DateTime dateUtc, DateTime nowUtc)
        {
            var diff = nowUtc - dateUtc;
            if (diff.TotalSeconds < 60) return "now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return dateUtc.ToLocalTime().ToString("d MMM");
        }
    }
}
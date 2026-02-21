using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Administrator.AdminPanel
{
    [Authorize(Roles = "Administrator")]
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

        public string AdminEmail { get; set; } = "";

        public int TotalUsersCount { get; set; }
        public int ActiveDoctorsCount { get; set; }
        public int RecentSecurityEvents { get; set; }
        public int UnreadNotifications { get; set; }

        public int StorageUsage { get; set; }

        public List<LoginEventVm> RecentLogins { get; set; } = new();
        public List<NewestUserVm> NewestUsers { get; set; } = new();

        public sealed class LoginEventVm
        {
            public string UserName { get; set; } = "";
            public string RoleOrScheme { get; set; } = "";
            public DateTime TimestampUtc { get; set; }
            public bool Success { get; set; }
        }

        public sealed class NewestUserVm
        {
            public string Id { get; set; } = "";
            public string Email { get; set; } = "";
            public DateTime? CreatedDateUtc { get; set; }
            public string Role { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var admin = await _userManager.GetUserAsync(User);
            AdminEmail = admin?.Email ?? admin?.UserName ?? "Admin";

            TotalUsersCount = await _context.Users.AsNoTracking().CountAsync();

            var doctorUsers = await _userManager.GetUsersInRoleAsync("Doctor");
            ActiveDoctorsCount = doctorUsers.Count(u => !IsLocked(u) && !u.IsSoftDeleted);

            UnreadNotifications = 0;
            if (admin != null)
            {
                UnreadNotifications = await _context.UserNotifications
                    .AsNoTracking()
                    .Where(n => n.UserId == admin.Id && !n.IsRead)
                    .CountAsync();
            }

            var nowUtc = DateTime.UtcNow;
            var sinceUtc = nowUtc.AddDays(-7);

            var fileEvents = ReadAuditEventsFromFiles(_env.ContentRootPath, sinceUtc);
            RecentSecurityEvents = fileEvents.Count;

            RecentLogins = fileEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.AuditType) && e.AuditType!.Contains("SignIn", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.TimestampUtc)
                .Take(12)
                .Select(e => new LoginEventVm
                {
                    UserName = string.IsNullOrWhiteSpace(e.UserName) ? (e.UserId ?? "Unknown") : e.UserName!,
                    RoleOrScheme = string.IsNullOrWhiteSpace(e.Scheme) ? "-" : e.Scheme!,
                    TimestampUtc = DateTime.SpecifyKind(e.TimestampUtc, DateTimeKind.Utc),
                    Success = e.Success
                })
                .ToList();

            NewestUsers = await BuildNewestUsersAsync();

            StorageUsage = ComputeUploadsUsagePercent(_env.WebRootPath);
        }

        private async Task<List<NewestUserVm>> BuildNewestUsersAsync()
        {
            try
            {
                var created = await _context.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.EventType == AuditEventType.Create && a.EntityName == nameof(ApplicationUser))
                    .OrderByDescending(a => a.OccurredAtUtc)
                    .Take(80)
                    .Select(a => new { a.EntityId, a.OccurredAtUtc })
                    .ToListAsync();

                if (created.Count == 0)
                    return await FallbackNewestUsersAsync();

                var orderedUserIds = created
                    .Where(x => !string.IsNullOrWhiteSpace(x.EntityId))
                    .Select(x => x.EntityId!)
                    .Distinct()
                    .ToList();

                if (orderedUserIds.Count == 0)
                    return await FallbackNewestUsersAsync();

                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => orderedUserIds.Contains(u.Id))
                    .ToListAsync();

                var byId = users.ToDictionary(u => u.Id, u => u, StringComparer.Ordinal);
                var createdByUserId = created
                    .Where(x => !string.IsNullOrWhiteSpace(x.EntityId))
                    .GroupBy(x => x.EntityId!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.OccurredAtUtc), StringComparer.Ordinal);

                var vms = new List<NewestUserVm>();
                foreach (var userId in orderedUserIds)
                {
                    if (!byId.TryGetValue(userId, out var u))
                        continue;

                    var roles = await _userManager.GetRolesAsync(u);
                    createdByUserId.TryGetValue(userId, out var when);

                    vms.Add(new NewestUserVm
                    {
                        Id = u.Id,
                        Email = u.Email ?? u.UserName ?? "(no email)",
                        CreatedDateUtc = when == default ? null : when,
                        Role = roles.FirstOrDefault() ?? "-"
                    });

                    if (vms.Count >= 8)
                        break;
                }

                if (vms.Count == 0)
                    return await FallbackNewestUsersAsync();

                return vms;
            }
            catch
            {
                return await FallbackNewestUsersAsync();
            }
        }

        private async Task<List<NewestUserVm>> FallbackNewestUsersAsync()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsSoftDeleted)
                .OrderByDescending(u => u.Id)
                .Take(8)
                .ToListAsync();

            var vms = new List<NewestUserVm>(users.Count);
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vms.Add(new NewestUserVm
                {
                    Id = u.Id,
                    Email = u.Email ?? u.UserName ?? "(no email)",
                    CreatedDateUtc = null,
                    Role = roles.FirstOrDefault() ?? "-"
                });
            }
            return vms;
        }

        private static bool IsLocked(ApplicationUser user)
            => user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        private static int ComputeUploadsUsagePercent(string webRootPath)
        {
            try
            {
                var uploads = Path.Combine(webRootPath, "uploads");
                if (!Directory.Exists(uploads))
                    return 0;

                long totalBytes = 0;
                foreach (var file in Directory.EnumerateFiles(uploads, "*", SearchOption.AllDirectories))
                {
                    try { totalBytes += new FileInfo(file).Length; } catch { }
                }

                const long capacityBytes = 5L * 1024L * 1024L * 1024L;
                var pct = (int)Math.Round(Math.Clamp((double)totalBytes / capacityBytes * 100.0, 0.0, 100.0));
                return pct;
            }
            catch
            {
                return 0;
            }
        }

        private sealed class AuditEvent
        {
            public DateTime TimestampUtc { get; set; }
            public string? AuditType { get; set; }
            public string? UserId { get; set; }
            public string? UserName { get; set; }
            public string? Scheme { get; set; }
            public bool Success { get; set; } = true;
        }

        private static List<AuditEvent> ReadAuditEventsFromFiles(string contentRootPath, DateTime sinceUtc)
        {
            var result = new List<AuditEvent>();

            try
            {
                var logsDir = Path.Combine(contentRootPath, "Logs");
                if (!Directory.Exists(logsDir))
                    return result;

                var files = Directory.EnumerateFiles(logsDir, "audit-*.json", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToList();

                foreach (var file in files)
                {
                    foreach (var line in System.IO.File.ReadLines(file))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;

                            var ts = TryGetString(root, "@t") ?? TryGetString(root, "Timestamp") ?? TryGetString(root, "Time");
                            if (!TryParseTimestampUtc(ts, out var tsUtc))
                                continue;

                            if (tsUtc < sinceUtc)
                                continue;

                            var auditType = TryGetString(root, "AuditType") ?? TryGetString(root, "EventType") ?? TryGetString(root, "Type");
                            var userId = TryGetString(root, "UserId");
                            var userName = TryGetString(root, "UserName") ?? TryGetString(root, "Email");
                            var scheme = TryGetString(root, "Scheme") ?? TryGetString(root, "AuthScheme") ?? TryGetString(root, "Provider");

                            bool success = true;
                            if (TryGetBool(root, "Success", out var b)) success = b;
                            else if (auditType != null && auditType.Contains("Fail", StringComparison.OrdinalIgnoreCase)) success = false;

                            result.Add(new AuditEvent
                            {
                                TimestampUtc = tsUtc,
                                AuditType = auditType,
                                UserId = userId,
                                UserName = userName,
                                Scheme = scheme,
                                Success = success
                            });
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return result;
        }

        private static string? TryGetString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var p))
                return null;

            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => p.ToString()
            };
        }

        private static bool TryGetBool(JsonElement root, string name, out bool value)
        {
            value = false;
            if (!root.TryGetProperty(name, out var p))
                return false;

            if (p.ValueKind == JsonValueKind.True) { value = true; return true; }
            if (p.ValueKind == JsonValueKind.False) { value = false; return true; }
            if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b)) { value = b; return true; }

            return false;
        }

        private static bool TryParseTimestampUtc(string? s, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(s))
                return false;

            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            {
                utc = dto.UtcDateTime;
                return true;
            }

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return true;
            }

            return false;
        }
    }
}

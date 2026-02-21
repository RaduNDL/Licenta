using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Globalization;

namespace Licenta.Pages.Administrator
{
    [Authorize(Roles = "Administrator")]
    public class AuditModel : PageModel
    {
        public List<SignInEntry> Logins { get; set; } = new();

        public class SignInEntry
        {
            public DateTime TimestampUtc { get; set; }
            public string? UserId { get; set; }
            public string? UserName { get; set; }
            public string? RemoteIp { get; set; }
            public string? Scheme { get; set; }
        }

        public async Task OnGetAsync()
        {
            Logins = await ReadSignInsAsync(max: 200);
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            var items = await ReadSignInsAsync(max: 5000);

            var sb = new StringBuilder();
            sb.AppendLine("TimestampUtc,TimestampRomania,UserId,UserName,RemoteIp,Scheme");

            foreach (var e in items.OrderByDescending(x => x.TimestampUtc))
            {
                var local = ToRomaniaLocal(e.TimestampUtc);

                var line = string.Join(",",
                    Quote(DateTime.SpecifyKind(e.TimestampUtc, DateTimeKind.Utc).ToString("O")),
                    Quote(local.ToString("O")),
                    Quote(e.UserId ?? ""),
                    Quote(e.UserName ?? ""),
                    Quote(e.RemoteIp ?? ""),
                    Quote(e.Scheme ?? "")
                );

                sb.AppendLine(line);
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "audit_signins.csv");
        }

        public DateTime ToRomaniaLocal(DateTime utc)
        {
            var tz = RomaniaTimeZone();
            var safeUtc = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(safeUtc, tz);
        }

        private static string Quote(string s)
            => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static TimeZoneInfo RomaniaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
            }
            catch
            {
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("E. Europe Standard Time");
            }
            catch
            {
            }

            return TimeZoneInfo.Local;
        }

        private async Task<List<SignInEntry>> ReadSignInsAsync(int max)
        {
            var result = new List<SignInEntry>();
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logsDir))
                return result;

            var files = Directory.GetFiles(logsDir, "audit-*.json")
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();

            foreach (var file in files)
            {
                List<string> lines;
                try
                {
                    lines = new List<string>();
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    string? line;
                    while ((line = await sr.ReadLineAsync()) != null)
                        lines.Add(line);
                }
                catch
                {
                    continue;
                }

                foreach (var line in lines.AsEnumerable().Reverse())
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        var auditType = TryGetString(root, "AuditType");
                        if (!string.Equals(auditType, "SignIn", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var tsStr =
                            TryGetString(root, "TimestampUtc")
                            ?? TryGetString(root, "@t")
                            ?? TryGetString(root, "Timestamp");

                        if (!TryParseTimestampToUtc(tsStr, out var tsUtc))
                            continue;

                        var entry = new SignInEntry
                        {
                            TimestampUtc = tsUtc,
                            UserId = TryGetString(root, "UserId"),
                            UserName = TryGetString(root, "UserName") ?? TryGetString(root, "Email"),
                            RemoteIp = TryGetString(root, "RemoteIp") ?? TryGetString(root, "Ip"),
                            Scheme = TryGetString(root, "Scheme") ?? TryGetString(root, "AuthScheme")
                        };

                        result.Add(entry);
                    }
                    catch
                    {
                    }

                    if (result.Count >= max)
                        return result;
                }
            }

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

        private static bool TryParseTimestampToUtc(string? s, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(s))
                return false;

            var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind;

            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, styles, out var dtoInv))
            {
                utc = dtoInv.UtcDateTime;
                return true;
            }

            if (DateTimeOffset.TryParse(s, CultureInfo.GetCultureInfo("ro-RO"), styles, out var dtoRo))
            {
                utc = dtoRo.UtcDateTime;
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

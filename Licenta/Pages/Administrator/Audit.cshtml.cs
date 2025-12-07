using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Licenta.Pages.Administrator
{
    [Authorize(Roles = "Administrator")]
    public class AuditModel : PageModel
    {
        public List<SignInEntry> Logins { get; set; } = new();

        public class SignInEntry
        {
            public DateTime Timestamp { get; set; }
            public string? UserId { get; set; }
            public string? UserName { get; set; }
            public string? RemoteIp { get; set; }
            public string? Scheme { get; set; }
        }

        public async Task OnGetAsync()
        {
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logsDir))
                return;

            var latestFile = Directory.GetFiles(logsDir, "audit-*.json")
                                      .OrderByDescending(f => f)
                                      .FirstOrDefault();
            if (latestFile == null)
                return;

            var lines = new List<string>();
            try
            {
                using var fs = new FileStream(latestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                string? line;
                while ((line = await sr.ReadLineAsync()) != null)
                    lines.Add(line);
            }
            catch
            {
                return;
            }

            foreach (var line in lines.AsEnumerable().Reverse())
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("AuditType", out var at) || at.GetString() != "SignIn")
                        continue;

                    var entry = new SignInEntry
                    {
                        Timestamp = root.TryGetProperty("@t", out var t) ? t.GetDateTime() : DateTime.UtcNow,
                        UserId = root.TryGetProperty("UserId", out var uid) ? uid.GetString() : null,
                        UserName = root.TryGetProperty("UserName", out var un) ? un.GetString() : null,
                        RemoteIp = root.TryGetProperty("RemoteIp", out var ip) ? ip.GetString() : null,
                        Scheme = root.TryGetProperty("Scheme", out var sch) ? sch.GetString() : null
                    };

                    Logins.Add(entry);
                }
                catch
                {
                }

                if (Logins.Count >= 200) break; 
            }
        }
    }
}

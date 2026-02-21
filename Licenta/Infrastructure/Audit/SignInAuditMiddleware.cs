using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Licenta.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Infrastructure.Audit
{
    public class SignInAuditMiddleware
    {
        private readonly RequestDelegate _next;

        public SignInAuditMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context, IHubContext<NotificationHub> hub)
        {
            await _next(context);

            if (context.User?.Identity?.IsAuthenticated != true)
                return;

            if (context.Session == null || !context.Session.IsAvailable)
                return;

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var key = $"audit:signin:{userId}";
            if (context.Session.GetString(key) == "1")
                return;

            context.Session.SetString(key, "1");

            var userName =
                context.User.Identity?.Name
                ?? context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.FindFirstValue(ClaimTypes.Name)
                ?? "unknown";

            var scheme = context.User.Identity?.AuthenticationType ?? "Identity.Application";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";
            var tsUtc = DateTime.UtcNow;

            Log.ForContext("AuditType", "SignIn")
               .ForContext("TimestampUtc", tsUtc.ToString("O"))
               .ForContext("UserId", userId)
               .ForContext("UserName", userName)
               .ForContext("RemoteIp", ip)
               .ForContext("Scheme", scheme)
               .Information("SignIn {UserName} from {RemoteIp} via {Scheme}", userName, ip, scheme);

            await hub.Clients.Group(NotificationHub.AuditAdminsGroup)
                .SendAsync("audit:signin", new
                {
                    timestampUtc = tsUtc.ToString("O"),
                    userId,
                    userName,
                    remoteIp = ip,
                    scheme
                });
        }
    }
}

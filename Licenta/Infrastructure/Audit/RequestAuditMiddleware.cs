using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Infrastructure.Audit
{
    public class RequestAuditMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestAuditMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/lib/") || path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/images/"))
            {
                await _next(context);
                return;
            }

            var sw = Stopwatch.StartNew();

            var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var userName = context.User?.Identity?.IsAuthenticated == true
                ? (context.User.Identity?.Name ?? "unknown")
                : "anonymous";

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();

                Log.ForContext("AuditType", "Request")
                   .ForContext("TimestampUtc", System.DateTime.UtcNow.ToString("O"))
                   .ForContext("UserId", userId)
                   .ForContext("UserName", userName)
                   .ForContext("Method", context.Request.Method)
                   .ForContext("Path", path)
                   .ForContext("StatusCode", context.Response?.StatusCode)
                   .ForContext("ElapsedMs", sw.ElapsedMilliseconds)
                   .Information("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                        context.Request.Method, path, context.Response?.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}

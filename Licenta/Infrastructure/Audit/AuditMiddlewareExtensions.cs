using Microsoft.AspNetCore.Builder;

namespace Infrastructure.Audit
{
    public static class AuditMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuditMiddleware(this IApplicationBuilder app)
        {
            app.UseMiddleware<SignInAuditMiddleware>();
            app.UseMiddleware<RequestAuditMiddleware>();
            return app;
        }
    }
}

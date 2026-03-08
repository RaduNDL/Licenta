using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Licenta.Services
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string AuditAdminsGroup = "AUDIT_ADMINS";

        private string? ResolveUserId()
        {
            return Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = ResolveUserId();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"USER_{userId}");

                if (Context.User?.IsInRole("Administrator") == true)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, AuditAdminsGroup);
                }
            }

            await base.OnConnectedAsync();
        }

        [Authorize(Roles = "Administrator")]
        public async Task JoinAuditAdmins()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AuditAdminsGroup);
        }
    }
}
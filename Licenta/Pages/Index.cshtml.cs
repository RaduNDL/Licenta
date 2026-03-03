using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages
{
    [Authorize]
    public class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        public sealed class LinkVm
        {
            public string Title { get; set; } = "";
            public string Url { get; set; } = "";
            public string Icon { get; set; } = "";
            public string Subtitle { get; set; } = "";
            public string KeyHint { get; set; } = "";
        }

        public string DisplayName { get; private set; } = "User";
        public string PrimaryRoleDisplay { get; private set; } = "User";
        public string PrimaryDestinationUrl { get; private set; } = "/";

        public List<LinkVm> LaunchCards { get; private set; } = [];
        public List<LinkVm> QuickLinks { get; private set; } = [];

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            DisplayName = user.FullName ?? user.Email ?? user.UserName ?? "User";

            var roles = await _userManager.GetRolesAsync(user);
            var role = PickPrimaryRole(roles);

            PrimaryRoleDisplay = role switch
            {
                "Administrator" => "Administrator",
                "Doctor" => "Doctor",
                "Assistant" => "Assistant",
                "Patient" => "Patient",
                _ => "User"
            };

            PrimaryDestinationUrl = role switch
            {
                "Administrator" => "/Administrator/AdminPanel/Index",
                "Doctor" => "/Doctor/Index",
                "Assistant" => "/Assistant/AssistantPanel/Index",
                "Patient" => "/Patient/Index",
                _ => "/"
            };

            LaunchCards = BuildLaunchCards(role);
            QuickLinks = BuildQuickLinks(role);
        }

        private static string PickPrimaryRole(IList<string> roles)
        {
            if (roles.Contains("Administrator")) return "Administrator";
            if (roles.Contains("Doctor")) return "Doctor";
            if (roles.Contains("Assistant")) return "Assistant";
            if (roles.Contains("Patient")) return "Patient";
            return "";
        }

        private static List<LinkVm> BuildLaunchCards(string role)
        {
            return role switch
            {
                "Doctor" =>
                [
                    new() { Title = "Appointments", Subtitle = "Calendar, approvals, reschedules", Url = "/Doctor/Appointments/Index", Icon = "📅" },
                    new() { Title = "AI Predictions", Subtitle = "Run models & review results", Url = "/Doctor/Predictions/Index", Icon = "🤖" },
                    new() { Title = "Attachments", Subtitle = "Inbox & uploads", Url = "/Doctor/Attachments/Inbox", Icon = "📎" },
                    new() { Title = "Medical Records", Subtitle = "Create & manage", Url = "/Doctor/MedicalRecords/Index", Icon = "🩺" }
                ],
                "Assistant" =>
                [
                    new() { Title = "Appointment Requests", Subtitle = "Process & schedule", Url = "/Assistant/Appointments/Requests", Icon = "🗂️" },
                    new() { Title = "Assistant Panel", Subtitle = "Overview & tools", Url = "/Assistant/AssistantPanel/Index", Icon = "🧭" },
                    new() { Title = "Patients", Subtitle = "Lookup & support", Url = "/Assistant/Patients/Index", Icon = "👥" },
                    new() { Title = "Inbox", Subtitle = "Pending items", Url = "/Assistant/Messages/Inbox", Icon = "📥" }
                ],
                "Administrator" =>
                [
                    new() { Title = "Admin Panel", Subtitle = "System overview", Url = "/Administrator/AdminPanel/Index", Icon = "🛡️" },
                    new() { Title = "Users", Subtitle = "Accounts & roles", Url = "/Administrator/Users/Index", Icon = "👤" },
                    new() { Title = "Settings", Subtitle = "Clinic configuration", Url = "/Administrator/Settings/Index", Icon = "⚙️" },
                    new() { Title = "Audit Logs", Subtitle = "Security trail", Url = "/Administrator/Audit", Icon = "🧾" }
                ],
                "Patient" =>
                [
                    new() { Title = "My Appointments", Subtitle = "Schedule & history", Url = "/Patient/Appointments/Index", Icon = "📅" },
                    new() { Title = "My Documents", Subtitle = "Attachments & results", Url = "/Patient/Attachments/Index", Icon = "📄" },
                    new() { Title = "Medical Records", Subtitle = "Visit details", Url = "/Patient/MedicalRecords/Index", Icon = "🩻" },
                    new() { Title = "Messages", Subtitle = "Clinic communication", Url = "/Patient/Messages/Inbox", Icon = "💬" }
                ],
                _ =>
                [
                    new() { Title = "Home", Subtitle = "Go to dashboard", Url = "/", Icon = "🏠" }
                ]
            };
        }

        private static List<LinkVm> BuildQuickLinks(string role)
        {
            return role switch
            {
                "Doctor" =>
                [
                    new() { Title = "Doctor Dashboard", Url = "/Doctor/Index", Icon = "🧑‍⚕️", KeyHint = "D" },
                    new() { Title = "Appointments", Url = "/Doctor/Appointments/Index", Icon = "📅", KeyHint = "A" },
                    new() { Title = "Approvals", Url = "/Doctor/Appointments/Approvals", Icon = "✅", KeyHint = "P" },
                    new() { Title = "Attachments Inbox", Url = "/Doctor/Attachments/Inbox", Icon = "📥", KeyHint = "I" },
                    new() { Title = "Run CBIS-DDSM", Url = "/Doctor/Predictions/CBISDDSM", Icon = "🤖", KeyHint = "R" }
                ],
                "Assistant" =>
                [
                    new() { Title = "Assistant Panel", Url = "/Assistant/AssistantPanel/Index", Icon = "🧭", KeyHint = "H" },
                    new() { Title = "Appointment Requests", Url = "/Assistant/Appointments/Requests", Icon = "🗂️", KeyHint = "A" },
                    new() { Title = "Schedule Process", Url = "/Assistant/Appointments/Process", Icon = "🕒", KeyHint = "S" },
                    new() { Title = "Inbox", Url = "/Assistant/Messages/Inbox", Icon = "📥", KeyHint = "I" }
                ],
                "Administrator" =>
                [
                    new() { Title = "Admin Panel", Url = "/Administrator/AdminPanel/Index", Icon = "🛡️", KeyHint = "H" },
                    new() { Title = "Overview", Url = "/Administrator/Overview/Index", Icon = "📊", KeyHint = "O" },
                    new() { Title = "Users", Url = "/Administrator/Users/Index", Icon = "👤", KeyHint = "U" },
                    new() { Title = "Settings", Url = "/Administrator/Settings/Index", Icon = "⚙️", KeyHint = "S" },
                    new() { Title = "Audit Logs", Url = "/Administrator/Audit", Icon = "🧾", KeyHint = "L" },
                    new() { Title = "Notifications", Url = "/Administrator/Notifications/Index", Icon = "🔔", KeyHint = "N" },
                    new() { Title = "Admin Profile", Url = "/Administrator/AdminProfile/Index", Icon = "👨‍💻", KeyHint = "P" }
                ],
                "Patient" =>
                [
                    new() { Title = "Patient Home", Url = "/Patient/Index", Icon = "🏠", KeyHint = "H" },
                    new() { Title = "Appointments", Url = "/Patient/Appointments/Index", Icon = "📅", KeyHint = "A" },
                    new() { Title = "Documents", Url = "/Patient/Attachments/Index", Icon = "📄", KeyHint = "D" },
                    new() { Title = "Medical Records", Url = "/Patient/MedicalRecords/Index", Icon = "🩺", KeyHint = "R" }
                ],
                _ => []
            };
        }
    }
}
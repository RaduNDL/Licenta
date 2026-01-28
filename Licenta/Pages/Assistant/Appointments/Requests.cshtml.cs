using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class AppointmentRequestsPageModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public AppointmentRequestsPageModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public class RequestVm
        {
            public Guid AttachmentId { get; set; }
            public string PatientName { get; set; } = "";
            public DateTime UploadedAt { get; set; }
            public string RequestedDisplay { get; set; } = "-";
            public string? Reason { get; set; }
        }

        private class AppointmentRequestPayload
        {
            public string SelectedLocalIso { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        public List<RequestVm> PendingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return;

            var clinicId = assistant.ClinicId;

            var attachments = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a =>
                    a.Type == "AppointmentRequest" &&
                    a.Status == AttachmentStatus.Pending &&
                    (string.IsNullOrWhiteSpace(clinicId) ||
                     a.Patient!.User!.ClinicId == clinicId))
                .OrderBy(a => a.UploadedAt)
                .ToListAsync();

            foreach (var att in attachments)
            {
                var payload = await ReadPayload(att.FilePath);

                PendingRequests.Add(new RequestVm
                {
                    AttachmentId = att.Id,
                    PatientName = att.Patient?.User?.FullName ?? att.Patient?.User?.Email ?? "Patient",
                    UploadedAt = att.UploadedAt,
                    RequestedDisplay = payload?.SelectedLocalIso ?? "-",
                    Reason = payload?.Reason ?? att.PatientNotes
                });
            }
        }

        private async Task<AppointmentRequestPayload?> ReadPayload(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var path = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));

            if (!System.IO.File.Exists(path))
                return null;

            var json = await System.IO.File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<AppointmentRequestPayload>(json);
        }
    }
}

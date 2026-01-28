using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class LaunchModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public LaunchModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? predictionId, string? modelName, Guid? patientId, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null)
                return Forbid();

            string resolvedModelName = (modelName ?? "").Trim();
            Guid? resolvedPatientId = patientId;

            if (predictionId.HasValue && predictionId.Value != Guid.Empty)
            {
                var p = await _db.Predictions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == predictionId.Value && x.DoctorId == doctor.Id, ct);

                if (p == null)
                {
                    TempData["StatusMessage"] = "Prediction record not found.";
                    return RedirectToPage("/Doctor/Predictions/Index");
                }

                resolvedModelName = (p.ModelName ?? "").Trim();
                resolvedPatientId = p.PatientId;

                if (TryExtractAttachmentId(p.InputDataJson, out var attachmentId))
                {
                    return RedirectToPage("/Doctor/Predictions/FromAttachment", new { id = attachmentId });
                }
            }

            var targetPage = ResolveModelPage(resolvedModelName);

            if (string.IsNullOrWhiteSpace(targetPage))
            {
                TempData["StatusMessage"] = $"No launch page mapped for model '{resolvedModelName}'.";
                return RedirectToPage("/Doctor/Predictions/Index");
            }

            return RedirectToPage(targetPage, new { patientId = resolvedPatientId });
        }

        private static bool TryExtractAttachmentId(string? inputDataJson, out Guid attachmentId)
        {
            attachmentId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(inputDataJson))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(inputDataJson);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return false;

                if (root.TryGetProperty("attachment_id", out var attEl))
                {
                    var raw = attEl.ValueKind == JsonValueKind.String ? attEl.GetString() : attEl.ToString();
                    if (Guid.TryParse(raw, out attachmentId) && attachmentId != Guid.Empty)
                        return true;
                }

                if (root.TryGetProperty("attachmentId", out var attEl2))
                {
                    var raw = attEl2.ValueKind == JsonValueKind.String ? attEl2.GetString() : attEl2.ToString();
                    if (Guid.TryParse(raw, out attachmentId) && attachmentId != Guid.Empty)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string? ResolveModelPage(string? modelName)
        {
            var m = (modelName ?? "").Trim();

            if (m.Equals("ISIC2019", StringComparison.OrdinalIgnoreCase) || m.Equals("ISIC 2019", StringComparison.OrdinalIgnoreCase))
                return "/Doctor/Predictions/ISIC2019";

            return null;
        }
    }
}
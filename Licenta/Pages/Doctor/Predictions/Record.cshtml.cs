using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services.Ml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class RecordModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlImagingClient _ml;

        public RecordModel(AppDbContext db, UserManager<ApplicationUser> userManager, IMlImagingClient ml)
        {
            _db = db;
            _userManager = userManager;
            _ml = ml;
        }

        public string ResultLabel { get; set; } = "-";
        public string Confidence { get; set; } = "-";
        public string ImageUrl { get; set; } = "";
        public string ModelName { get; set; } = "-";
        public DateTime CreatedAtUtc { get; set; }
        public string PatientName { get; set; } = "-";
        public string ErrorMessage { get; set; } = string.Empty;

        public Dictionary<string, float> Probabilities { get; set; } = new();

        public string? DatasetLabel { get; set; }
        public string? ValidationStatus { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
                return Forbid();

            var prediction = await _db.Predictions
                .Include(p => p.Patient)
                .ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(p => p.Id == id && p.DoctorId == doctor.Id);

            if (prediction == null)
                return NotFound();

            CreatedAtUtc = prediction.CreatedAtUtc;
            ModelName = prediction.ModelName ?? "-";
            PatientName = prediction.Patient?.User?.FullName ?? "Unknown Patient";

            if (string.IsNullOrWhiteSpace(prediction.OutputDataJson))
            {
                ErrorMessage = "No AI output stored for this prediction.";
                return Page();
            }

            try
            {
                using var doc = JsonDocument.Parse(prediction.OutputDataJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("label", out var labelProp) && labelProp.ValueKind == JsonValueKind.String)
                    ResultLabel = labelProp.GetString() ?? "-";

                if (root.TryGetProperty("effective_probability", out var probProp) && probProp.ValueKind == JsonValueKind.Number)
                    Confidence = probProp.GetDouble().ToString("P1");
                else if (root.TryGetProperty("effectiveProbability", out var probProp2) && probProp2.ValueKind == JsonValueKind.Number)
                    Confidence = probProp2.GetDouble().ToString("P1");

                if (root.TryGetProperty("probabilities", out var probsProp) && probsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in probsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Number)
                            Probabilities[prop.Name] = (float)prop.Value.GetDouble();
                    }
                }

                if (root.TryGetProperty("ground_truth", out var gtProp) && gtProp.ValueKind == JsonValueKind.String)
                    DatasetLabel = gtProp.GetString();

                Guid? attachmentId = TryGetAttachmentId(prediction.InputDataJson);

                if (attachmentId.HasValue)
                {
                    var attachment = await _db.MedicalAttachments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Id == attachmentId.Value);

                    if (attachment != null)
                    {
                        ImageUrl = Url.Page("/Files/Attachment", new { id = attachment.Id }) ?? "";

                        if (string.IsNullOrEmpty(DatasetLabel) && !string.IsNullOrEmpty(attachment.FileName))
                        {
                            var gt = await _ml.GetGroundTruthAsync(attachment.FileName, HttpContext.RequestAborted);
                            DatasetLabel = gt.GroundTruth;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(DatasetLabel) && !string.IsNullOrWhiteSpace(ResultLabel))
                {
                    var isMatch = string.Equals(DatasetLabel, ResultLabel, StringComparison.OrdinalIgnoreCase);
                    ValidationStatus = isMatch ? "CORRECT" : "INCORRECT";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to read saved AI result: {ex.Message}";
            }

            return Page();
        }

        private static Guid? TryGetAttachmentId(string? inputDataJson)
        {
            if (string.IsNullOrWhiteSpace(inputDataJson))
                return null;

            try
            {
                using var inputDoc = JsonDocument.Parse(inputDataJson);
                var root = inputDoc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                if (root.TryGetProperty("attachment_id", out var att) || root.TryGetProperty("attachmentId", out att))
                {
                    var raw = att.ValueKind == JsonValueKind.String ? att.GetString() : att.ToString();
                    if (Guid.TryParse(raw, out var g) && g != Guid.Empty)
                        return g;
                }
            }
            catch { }

            return null;
        }
    }
}

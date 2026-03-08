using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services.Ml;
using Licenta.Services.Ml.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class FromAttachmentModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlImagingClient _ml;
        private readonly IWebHostEnvironment _env;
        private readonly MlServiceOptions _mlOpt;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions { WriteIndented = true };

        public FromAttachmentModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IMlImagingClient ml,
            IWebHostEnvironment env,
            IOptions<MlServiceOptions> mlOpt)
        {
            _db = db;
            _userManager = userManager;
            _ml = ml;
            _env = env;
            _mlOpt = mlOpt.Value;
        }

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        [BindProperty]
        public string ModelId { get; set; } = "cbis_ddsm_images:torch_cnn";

        [BindProperty]
        public int ImageSize { get; set; } = 224;

        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var att = await _db.MedicalAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == Id, ct);
            if (att == null)
            {
                ErrorMessage = "Attachment not found.";
                return Page();
            }

            StatusMessage = $"Attachment loaded: {att.FileName}";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null)
                return Forbid();

            var att = await _db.MedicalAttachments.FirstOrDefaultAsync(a => a.Id == Id, ct);
            if (att == null)
            {
                ErrorMessage = "Attachment not found.";
                return Page();
            }

            var patient = await _db.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == att.PatientId, ct);
            if (patient == null)
            {
                ErrorMessage = "Patient not found for this attachment.";
                return Page();
            }

            var relPath = (att.FilePath ?? "").TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
            var physicalPath = Path.Combine(_env.WebRootPath, relPath);

            if (!System.IO.File.Exists(physicalPath))
            {
                ErrorMessage = "Attachment file is missing on disk.";
                return Page();
            }

            var effectiveModelId = string.IsNullOrWhiteSpace(ModelId) ? "cbis_ddsm_images:torch_cnn" : ModelId.Trim();
            var effectiveImageSize = ImageSize <= 0 ? 224 : ImageSize;

            var requireDomain = _mlOpt.RequireDomainGateImaging;
            var requireQuality = false;

            ImagingPredictResponse result;
            await using (var fs = System.IO.File.OpenRead(physicalPath))
            {
                result = await _ml.PredictImagingAsync(
                    fs,
                    att.FileName ?? "image",
                    string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType,
                    effectiveModelId,
                    effectiveImageSize,
                    requireQuality,
                    requireDomain,
                    ct);
            }

            if (IsRejected(result))
            {
                ErrorMessage = BuildRejectMessage(result);
                return Page();
            }

            var inputJson = JsonSerializer.Serialize(new
            {
                source = "attachment",
                model_id = effectiveModelId,
                image_size = effectiveImageSize,
                file_name = att.FileName,
                content_type = att.ContentType,
                attachment_id = att.Id
            }, JsonOpts);

            var outputJson = JsonSerializer.Serialize(result, JsonOpts);

            var safeLabel = string.IsNullOrWhiteSpace(result.Label) ? "UNKNOWN" : result.Label;

            var pred = new Prediction
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                CreatedAtUtc = DateTime.UtcNow,
                ModelName = "CBIS-DDSM",
                InputSummary = att.FileName,
                ResultLabel = safeLabel,
                Probability = (float?)result.EffectiveProbabilitySafe,
                InputDataJson = inputJson,
                OutputDataJson = outputJson,
                Status = PredictionStatus.Draft
            };

            _db.Predictions.Add(pred);
            await _db.SaveChangesAsync(ct);

            TempData["StatusMessage"] = "Prediction completed and saved.";
            return RedirectToPage("/Doctor/Predictions/Record", new { id = pred.Id });
        }

        private static bool IsRejected(ImagingPredictResponse? r)
        {
            if (r == null) return true;

            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "OUT_OF_DOMAIN", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "UNUSABLE_IMAGE", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string BuildRejectMessage(ImagingPredictResponse r)
        {
            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "UNUSABLE_IMAGE", StringComparison.OrdinalIgnoreCase))
            {
                var issues = (r.QualityIssues != null && r.QualityIssues.Count > 0)
                    ? string.Join(", ", r.QualityIssues)
                    : "unknown_quality_issue";

                return $"AI rejected: image quality is not acceptable ({issues}).";
            }

            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "OUT_OF_DOMAIN", StringComparison.OrdinalIgnoreCase))
            {
                var issues = (r.DomainIssues != null && r.DomainIssues.Count > 0)
                    ? string.Join(", ", r.DomainIssues)
                    : "out_of_domain";

                return $"AI rejected: uploaded image does not look like a breast mammogram ({issues}).";
            }

            return "AI rejected: uploaded image is not acceptable.";
        }
    }
}

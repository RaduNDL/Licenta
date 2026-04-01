using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services.Ml;
using Licenta.Services.Ml.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class CBISDDSMModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlImagingClient _ml;
        private readonly IWebHostEnvironment _env;
        private readonly MlServiceOptions _mlOpt;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private const string DefaultModelId = "mammogram_mastery_images:torch_cnn";

        public CBISDDSMModel(
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
        public Guid? PatientId { get; set; }

        [BindProperty]
        public IFormFile? Upload { get; set; }

        [BindProperty]
        public string ModelId { get; set; } = DefaultModelId;

        [BindProperty]
        public int ImageSize { get; set; } = 224;

        public PatientProfile? Patient { get; set; }
        public ImagingPredictResponse? Result { get; set; }

        public string? ErrorMessage { get; set; }
        public string? SavedPredictionId { get; set; }

        public string? TrainingBanner { get; set; }
        public string? TrainingDetails { get; set; }

        public List<SelectListItem> Patients { get; set; } = new();

        public async Task OnGetAsync(CancellationToken ct)
        {
            await LoadPatientsAsync(ct);
            await RefreshTrainingBannerAsync(ct);

            if (PatientId.HasValue && PatientId.Value != Guid.Empty)
                Patient = await LoadPatientAsync(PatientId.Value, ct);
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            await LoadPatientsAsync(ct);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null)
                return Forbid();

            if (!PatientId.HasValue || PatientId.Value == Guid.Empty)
            {
                ErrorMessage = "Please select a patient.";
                await RefreshTrainingBannerAsync(ct);
                return Page();
            }

            Patient = await LoadPatientAsync(PatientId.Value, ct);
            if (Patient == null)
            {
                ErrorMessage = "Selected patient not found.";
                await RefreshTrainingBannerAsync(ct);
                return Page();
            }

            if (Upload == null || Upload.Length == 0)
            {
                ErrorMessage = "Please upload an image file.";
                await RefreshTrainingBannerAsync(ct);
                return Page();
            }

            var tr = await SafeGetTrainingStateAsync(ct);
            if (tr != null && !tr.ArtifactOk)
            {
                TrainingBanner = BuildTrainingBanner(tr);
                TrainingDetails = BuildTrainingDetails(tr);
                ErrorMessage = "Model is not ready yet.";
                return Page();
            }

            try
            {
                var effectiveModelId = NormalizeModelId(ModelId);
                var effectiveImageSize = ImageSize <= 0 ? 224 : ImageSize;
                var requireDomain = _mlOpt.RequireDomainGateImaging;
                var requireQuality = false;

                byte[] fileBytes;
                await using (var inputMs = new MemoryStream())
                {
                    await Upload.CopyToAsync(inputMs, ct);
                    fileBytes = inputMs.ToArray();
                }

                if (fileBytes.Length == 0)
                {
                    ErrorMessage = "Uploaded file is empty.";
                    await RefreshTrainingBannerAsync(ct);
                    return Page();
                }

                await using var predictMs = new MemoryStream(fileBytes);

                Result = await _ml.PredictImagingAsync(
                    predictMs,
                    Upload.FileName ?? "image",
                    string.IsNullOrWhiteSpace(Upload.ContentType) ? "application/octet-stream" : Upload.ContentType,
                    effectiveModelId,
                    effectiveImageSize,
                    requireQuality,
                    requireDomain,
                    ct);

                if (Result == null)
                    throw new Exception("ML service returned empty result.");

                if (IsRejected(Result))
                {
                    await SaveDoctorImageAsync(
                        Patient.Id,
                        Upload,
                        fileBytes,
                        AttachmentStatus.Rejected,
                        doctor.Id,
                        BuildRejectMessage(Result),
                        ct);

                    ErrorMessage = BuildRejectMessage(Result);
                    await RefreshTrainingBannerAsync(ct);
                    return Page();
                }

                var attachmentId = await SaveDoctorImageAsync(
                    Patient.Id,
                    Upload,
                    fileBytes,
                    AttachmentStatus.Validated,
                    doctor.Id,
                    "AI prediction accepted.",
                    ct);

                var id = await SavePredictionAsync(
                    Patient,
                    doctor,
                    Result,
                    effectiveModelId,
                    effectiveImageSize,
                    attachmentId,
                    Upload,
                    ct);

                SavedPredictionId = id.ToString();
                TempData["StatusMessage"] = "Prediction completed and saved.";

                return RedirectToPage("/Doctor/Predictions/Record", new { id });
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                await RefreshTrainingBannerAsync(ct);
                return Page();
            }
        }

        private async Task<Guid> SaveDoctorImageAsync(
            Guid patientId,
            IFormFile file,
            byte[] fileBytes,
            AttachmentStatus status,
            Guid doctorId,
            string? validationNotes,
            CancellationToken ct)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "cbis_ddsm");
            Directory.CreateDirectory(uploadsFolder);

            var originalName = Path.GetFileName(file.FileName ?? "image");
            var uniqueFileName = $"{Guid.NewGuid()}_{originalName}";
            var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

            await System.IO.File.WriteAllBytesAsync(physicalPath, fileBytes, ct);

            var utcNow = DateTime.UtcNow;

            var attachment = new MedicalAttachment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                DoctorId = doctorId,
                ValidatedByDoctorId = doctorId,
                ValidatedAtUtc = utcNow,
                FileName = originalName,
                FilePath = $"/uploads/cbis_ddsm/{uniqueFileName}",
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Type = "CBIS-DDSM Breast Image (Doctor Upload)",
                Status = status,
                UploadedAt = utcNow,
                ValidationNotes = validationNotes
            };

            _db.MedicalAttachments.Add(attachment);
            await _db.SaveChangesAsync(ct);

            return attachment.Id;
        }

        private async Task<Guid> SavePredictionAsync(
            PatientProfile patient,
            global::Licenta.Models.DoctorProfile doctor,
            ImagingPredictResponse resp,
            string modelId,
            int imageSize,
            Guid attachmentId,
            IFormFile upload,
            CancellationToken ct)
        {
            var inputJson = JsonSerializer.Serialize(new
            {
                source = "doctor_upload",
                model_id = modelId,
                image_size = imageSize,
                file_name = upload?.FileName,
                content_type = upload?.ContentType,
                attachment_id = attachmentId
            }, JsonOpts);

            var outputJson = JsonSerializer.Serialize(resp, JsonOpts);

            var safeLabel = string.IsNullOrWhiteSpace(resp.Label) ? "UNKNOWN" : resp.Label;

            var prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                CreatedAtUtc = DateTime.UtcNow,
                ModelName = modelId.StartsWith("mammogram_mastery", StringComparison.OrdinalIgnoreCase)
                    ? "Mammogram Mastery"
                    : "CBIS-DDSM",
                InputSummary = upload?.FileName,
                ResultLabel = safeLabel,
                Probability = (float)resp.EffectiveProbabilitySafe,
                InputDataJson = inputJson,
                OutputDataJson = outputJson,
                Status = PredictionStatus.Accepted,
                ValidatedAtUtc = DateTime.UtcNow
            };

            _db.Predictions.Add(prediction);
            await _db.SaveChangesAsync(ct);

            return prediction.Id;
        }

        private async Task LoadPatientsAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            var clinicId = user?.ClinicId;

            var patientRoleId = await _db.Roles
                .Where(r => r.Name == "Patient")
                .Select(r => r.Id)
                .FirstOrDefaultAsync(ct);

            var patientUserIds = _db.UserRoles
                .Where(ur => ur.RoleId == patientRoleId)
                .Select(ur => ur.UserId);

            var query = _db.Patients
                .Include(p => p.User)
                .AsNoTracking()
                .Where(p => p.User != null && patientUserIds.Contains(p.UserId));

            if (!string.IsNullOrWhiteSpace(clinicId))
                query = query.Where(p => p.User != null && p.User.ClinicId == clinicId);

            Patients = await query
                .OrderBy(p => p.User!.FullName)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(p.User!.FullName) ? p.User.Email! : p.User.FullName!
                })
                .ToListAsync(ct);
        }

        private Task<PatientProfile?> LoadPatientAsync(Guid id, CancellationToken ct)
        {
            return _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        private async Task<MlTrainingState?> SafeGetTrainingStateAsync(CancellationToken ct)
        {
            try
            {
                var st = await _ml.GetStatusAsync(ct);
                return st.Training;
            }
            catch
            {
                return null;
            }
        }

        private static string BuildTrainingBanner(MlTrainingState? tr)
        {
            if (tr == null)
                return "ML status is unavailable.";

            if (!string.IsNullOrWhiteSpace(tr.Error))
                return "Model training failed.";

            if (tr.Started && !tr.Done)
                return "Model training is in progress.";

            return "Model is not ready yet.";
        }

        private static string? BuildTrainingDetails(MlTrainingState? tr)
        {
            if (tr == null)
                return null;

            if (!string.IsNullOrWhiteSpace(tr.Error))
                return tr.Error;

            if (!string.IsNullOrWhiteSpace(tr.ArtifactPath))
                return $"Artifact path: {tr.ArtifactPath}";

            return null;
        }

        private async Task RefreshTrainingBannerAsync(CancellationToken ct)
        {
            try
            {
                var st = await _ml.GetStatusAsync(ct);
                var tr = st?.Training;

                if (tr != null && tr.ArtifactOk)
                {
                    TrainingBanner = null;
                    TrainingDetails = null;
                    return;
                }

                TrainingBanner = BuildTrainingBanner(tr);
                TrainingDetails = BuildTrainingDetails(tr);

                if (tr == null)
                    TrainingDetails ??= "Status returned, but training info could not be read.";
            }
            catch (Exception ex)
            {
                TrainingBanner = "ML status is unavailable.";
                TrainingDetails = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static bool IsRejected(ImagingPredictResponse? r)
        {
            if (r == null)
                return true;

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

        private static string NormalizeModelId(string? raw)
        {
            var s = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s))
                return DefaultModelId;

            s = s.Replace(" ", "");

            if (s.Equals("cbis_ddsm_imagestorch_cnn", StringComparison.OrdinalIgnoreCase))
                return "cbis_ddsm_images:torch_cnn";

            if (s.Equals("mammogram_mastery_imagestorch_cnn", StringComparison.OrdinalIgnoreCase))
                return "mammogram_mastery_images:torch_cnn";

            if (!s.Contains(":") && s.EndsWith("torch_cnn", StringComparison.OrdinalIgnoreCase))
            {
                if (s.Contains("cbis_ddsm_images", StringComparison.OrdinalIgnoreCase))
                    return "cbis_ddsm_images:torch_cnn";

                if (s.Contains("mammogram_mastery_images", StringComparison.OrdinalIgnoreCase))
                    return "mammogram_mastery_images:torch_cnn";
            }

            return s;
        }
    }
}
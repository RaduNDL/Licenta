using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services.Ml;
using Licenta.Services.Ml.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
    public class ISIC2019Model : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlImagingClient _ml;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public ISIC2019Model(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IMlImagingClient ml)
        {
            _db = db;
            _userManager = userManager;
            _ml = ml;
        }

        [BindProperty(SupportsGet = true)]
        public Guid? PatientId { get; set; }

        [BindProperty]
        public IFormFile? File { get; set; }

        [BindProperty]
        public string ModelId { get; set; } = "isic_2019_images:torch_cnn";

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

            if (!string.IsNullOrWhiteSpace(user.ClinicId))
            {
                if (Patient.User == null || Patient.User.ClinicId != user.ClinicId)
                {
                    ErrorMessage = "Selected patient is not available in your clinic scope.";
                    await RefreshTrainingBannerAsync(ct);
                    return Page();
                }
            }

            if (File == null || File.Length == 0)
            {
                ErrorMessage = "Please upload an image file.";
                await RefreshTrainingBannerAsync(ct);
                return Page();
            }

            var tr = await SafeGetTrainingStateAsync(ct);
            if (tr == null || !tr.ArtifactOk)
            {
                TrainingBanner = BuildTrainingBanner(tr);
                TrainingDetails = BuildTrainingDetails(tr);
                ErrorMessage = tr != null && !string.IsNullOrWhiteSpace(tr.Error)
                    ? $"Model is not ready: {tr.Error}"
                    : "Model is not ready yet. Please refresh and retry.";
                return Page();
            }

            try
            {
                await using var ms = new MemoryStream();
                await File.CopyToAsync(ms, ct);
                ms.Position = 0;

                var effectiveModelId = string.IsNullOrWhiteSpace(ModelId) ? "isic_2019_images:torch_cnn" : ModelId.Trim();
                var effectiveImageSize = ImageSize <= 0 ? 224 : ImageSize;

                Result = await _ml.PredictImagingAsync(
                    ms,
                    string.IsNullOrWhiteSpace(File.FileName) ? "image.jpg" : File.FileName,
                    string.IsNullOrWhiteSpace(File.ContentType) ? "application/octet-stream" : File.ContentType,
                    effectiveModelId,
                    effectiveImageSize,
                    ct);

                if (Result == null || string.IsNullOrWhiteSpace(Result.Label))
                    throw new InvalidOperationException("ML returned an empty response.");

                var id = await SavePredictionAsync(Patient, doctor, Result, effectiveModelId, effectiveImageSize, ct);
                SavedPredictionId = id.ToString();

                TempData["StatusMessage"] = "Prediction completed and saved.";
                return RedirectToPage("/Doctor/Predictions/Record", new { id });
            }
            catch (Exception ex)
            {
                ErrorMessage = NormalizeMlError(ex.Message);
                await RefreshTrainingBannerAsync(ct);
                return Page();
            }
        }

        private async Task RefreshTrainingBannerAsync(CancellationToken ct)
        {
            var tr = await SafeGetTrainingStateAsync(ct);

            if (tr == null)
            {
                TrainingBanner = "ML status is unavailable.";
                TrainingDetails = null;
                return;
            }

            if (tr.ArtifactOk)
            {
                TrainingBanner = null;
                TrainingDetails = null;
                return;
            }

            TrainingBanner = BuildTrainingBanner(tr);
            TrainingDetails = BuildTrainingDetails(tr);
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

        private static string NormalizeMlError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "An unknown error occurred.";

            if (message.Contains("503", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("Model not ready", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("not available", StringComparison.OrdinalIgnoreCase)))
            {
                return "The ML model is still training or not ready. Please retry in a moment.";
            }

            return message;
        }

        private async Task LoadPatientsAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            var clinicId = user?.ClinicId;

            var q = _db.Patients
                .Include(p => p.User)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(clinicId))
            {
                q = q.Where(p => p.User != null && p.User.ClinicId == clinicId);
            }

            Patients = await q
                .OrderBy(p => p.User.FullName)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.User.FullName ?? p.User.Email
                })
                .ToListAsync(ct);
        }

        private Task<PatientProfile?> LoadPatientAsync(Guid id, CancellationToken ct)
        {
            return _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        private async Task<Guid> SavePredictionAsync(
            PatientProfile patient,
            Licenta.Models.DoctorProfile doctor,
            ImagingPredictResponse resp,
            string effectiveModelId,
            int effectiveImageSize,
            CancellationToken ct)
        {
            var inputJson = JsonSerializer.Serialize(new
            {
                source = "upload",
                model_id = effectiveModelId,
                image_size = effectiveImageSize,
                file_name = File?.FileName,
                content_type = File?.ContentType
            }, JsonOpts);

            var outputJson = JsonSerializer.Serialize(resp, JsonOpts);

            var p = new Prediction
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                CreatedAtUtc = DateTime.UtcNow,
                ModelName = "ISIC2019",
                InputSummary = File?.FileName,
                ResultLabel = resp.Label,
                Probability = resp.EffectiveProbability,
                InputDataJson = inputJson,
                OutputDataJson = outputJson,
                Status = PredictionStatus.Draft
            };

            _db.Predictions.Add(p);
            await _db.SaveChangesAsync(ct);

            return p.Id;
        }
    }
}
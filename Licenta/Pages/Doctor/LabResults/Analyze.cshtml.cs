using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services.Ml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Licenta.Pages.Doctor.LabResults
{
    [Authorize(Roles = "Doctor")]
    public class AnalyzeModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlLabResultClient _mlClient;
        private readonly ILogger<AnalyzeModel> _logger;

        public AnalyzeModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IMlLabResultClient mlClient,
            ILogger<AnalyzeModel> logger)
        {
            _db = db;
            _userManager = userManager;
            _mlClient = mlClient;
            _logger = logger;
        }

        public LabResult? LabResult { get; private set; }

        public string? PredictedLabel { get; private set; }
        public float? PredictedProbability { get; private set; }
        public string? Explanation { get; private set; }

        public bool HasPrediction => PredictedLabel != null;
        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                ErrorMessage = "Doctor profile not found.";
                return Page();
            }

            LabResult = await _db.LabResults
                .Include(l => l.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    (string.IsNullOrWhiteSpace(user.ClinicId) ||
                     l.Patient.User.ClinicId == user.ClinicId));

            if (LabResult == null)
                ErrorMessage = "Lab result not found.";

            return Page();
        }

        public async Task<IActionResult> OnPostAnalyzeAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            var lab = await _db.LabResults
                .Include(l => l.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lab == null) return NotFound();

            try
            {
                await using var stream = System.IO.File.OpenRead(lab.FilePath);

                var prediction = await _mlClient.AnalyzeLabResultAsync(
                    lab,
                    stream,
                    lab.FileName,
                    lab.ContentType);

                PredictedLabel = prediction.Label;
                PredictedProbability = prediction.Probability;
                Explanation = prediction.Explanation;

                lab.Notes = $"ML: {prediction.Label} ({(prediction.Probability ?? 0f):P0}) - {prediction.Explanation}";
                lab.Status = LabResultStatus.Validated;
                lab.ValidatedByDoctorId = doctor.Id;
                lab.ValidatedAtUtc = DateTime.UtcNow;

                _db.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    PatientId = lab.PatientId,
                    DoctorId = doctor.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModelName = "LabResultAI",
                    InputSummary = $"LabResult #{lab.Id}",
                    ResultLabel = prediction.Label,
                    Probability = prediction.Probability,
                    InputDataJson = JsonSerializer.Serialize(new { lab.Id, lab.FileName }),
                    OutputDataJson = JsonSerializer.Serialize(prediction),
                    Status = PredictionStatus.Draft
                });

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ML error");
                ErrorMessage = "ML service error.";
            }

            return RedirectToPage(new { id });
        }
    }
}

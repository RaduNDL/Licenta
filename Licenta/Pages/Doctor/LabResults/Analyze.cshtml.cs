using System;
using System.IO;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
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
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlLabResultClient _mlClient;
        private readonly ILogger<AnalyzeModel> _logger;

        public AnalyzeModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IMlLabResultClient mlClient,
            ILogger<AnalyzeModel> logger)
        {
            _context = context;
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
            if (user == null)
                return Challenge();

            var doctor = await _context.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null)
            {
                ErrorMessage = "Doctor profile not found.";
                return Page();
            }

            var lab = await _context.LabResults
                .Include(l => l.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lab == null)
            {
                ErrorMessage = "Lab result not found.";
                return Page();
            }

            LabResult = lab;

            if (string.IsNullOrWhiteSpace(lab.FilePath) ||
                string.IsNullOrWhiteSpace(lab.FileName) ||
                string.IsNullOrWhiteSpace(lab.ContentType))
            {
                ErrorMessage = "Lab result file information is incomplete.";
                return Page();
            }

            if (!System.IO.File.Exists(lab.FilePath))
            {
                ErrorMessage = "File not found on server.";
                return Page();
            }

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

                lab.Notes = $"ML: {prediction.Label} ({prediction.Probability:P0}) - {prediction.Explanation}";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling ML service for LabResult {LabResultId}", lab.Id);
                ErrorMessage = "An error occurred while contacting the ML service.";
            }

            return Page();
        }
    }
}
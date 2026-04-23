using Licenta.Areas.Identity.Data;
using Licenta.Data;
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

namespace Licenta.Pages.Patient.Predictions
{
    [Authorize(Roles = "Patient")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public Prediction? Item { get; private set; }

        public string MedicalType { get; set; } = "-";
        public string RiskLevel { get; set; } = "-";
        public string MedicalNote { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty)
            {
                TempData["StatusMessage"] = "Invalid prediction id.";
                return RedirectToPage("/Patient/Predictions/Index");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

            if (patient == null)
            {
                TempData["StatusMessage"] = "No patient profile linked to your account.";
                return RedirectToPage("/Patient/Dashboard/Index");
            }

            var prediction = await _db.Predictions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (prediction == null)
            {
                TempData["StatusMessage"] = "Prediction not found.";
                return RedirectToPage("/Patient/Predictions/Index");
            }

            if (prediction.PatientId != patient.Id)
            {
                TempData["StatusMessage"] = "Prediction not found.";
                return RedirectToPage("/Patient/Predictions/Index");
            }

            if (prediction.Status != PredictionStatus.Accepted)
            {
                TempData["StatusMessage"] = "This prediction is pending doctor validation.";
                return RedirectToPage("/Patient/Predictions/Index");
            }

            Item = prediction;
            ParseMedicalInfo(Item.OutputDataJson);

            return Page();
        }

        private void ParseMedicalInfo(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("extras", out var extras))
                    return;

                if (extras.ValueKind != JsonValueKind.Object)
                    return;

                if (extras.TryGetProperty("medical_type", out var mt) && mt.ValueKind == JsonValueKind.String)
                    MedicalType = mt.GetString() ?? "-";

                if (extras.TryGetProperty("risk_level", out var rl) && rl.ValueKind == JsonValueKind.String)
                    RiskLevel = rl.GetString() ?? "-";

                if (extras.TryGetProperty("medical_note", out var mn) && mn.ValueKind == JsonValueKind.String)
                    MedicalNote = mn.GetString() ?? "";
            }
            catch (JsonException)
            {
            }
        }
    }
}
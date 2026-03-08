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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

            if (patient == null)
                return RedirectToPage("/Patient/Dashboard/Index");

            Item = await _db.Predictions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.PatientId == patient.Id, ct);

            if (Item == null)
                return NotFound();

            if (Item.Status != PredictionStatus.Accepted)
            {
                TempData["StatusMessage"] = "This prediction is pending doctor validation.";
                return RedirectToPage("/Patient/Predictions/Index");
            }

            ParseMedicalInfo(Item.OutputDataJson);

            return Page();
        }

        private void ParseMedicalInfo(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("extras", out var extras))
                {
                    if (extras.TryGetProperty("medical_type", out var mt))
                        MedicalType = mt.GetString() ?? "-";

                    if (extras.TryGetProperty("risk_level", out var rl))
                        RiskLevel = rl.GetString() ?? "-";

                    if (extras.TryGetProperty("medical_note", out var mn))
                        MedicalNote = mn.GetString() ?? "";
                }
            }
            catch { }
        }
    }
}
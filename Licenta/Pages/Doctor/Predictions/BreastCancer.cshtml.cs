using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services.Ml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class BreastCancerModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBreastCancerClient _breastClient;

        public BreastCancerModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IBreastCancerClient breastClient)
        {
            _db = db;
            _userManager = userManager;
            _breastClient = breastClient;
        }

        public class InputModel
        {
            public Guid PatientId { get; set; }

            public float Radius_mean { get; set; }
            public float Texture_mean { get; set; }
            public float perimeter_mean { get; set; }
            public float area_mean { get; set; }
            public float smoothness_mean { get; set; }
            public float compactness_mean { get; set; }
            public float concavity_mean { get; set; }
            public float concavepoints_mean { get; set; }
            public float symmetry_mean { get; set; }
            public float fractal_dimension_mean { get; set; }

            public string? Notes { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<SelectListItem> Patients { get; set; } = new();

        public async Task OnGetAsync(Guid? patientId)
        {
            await LoadPatientsAsync(patientId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadPatientsAsync(Input.PatientId);

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
                return Unauthorized();

            var request = new BreastCancerRequest
            {
                Radius_mean = Input.Radius_mean,
                Texture_mean = Input.Texture_mean,
                perimeter_mean = Input.perimeter_mean,
                area_mean = Input.area_mean,
                smoothness_mean = Input.smoothness_mean,
                compactness_mean = Input.compactness_mean,
                concavity_mean = Input.concavity_mean,
                concavepoints_mean = Input.concavepoints_mean,
                symmetry_mean = Input.symmetry_mean,
                fractal_dimension_mean = Input.fractal_dimension_mean
            };

            BreastPredictionResponse response;
            try
            {
                response = await _breastClient.PredictAsync(request);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"ML service error: {ex.Message}");
                return Page();
            }

            var prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                PatientId = Input.PatientId,
                DoctorId = doctor.Id,
                CreatedAtUtc = DateTime.UtcNow,
                ModelName = response.RawModelName ?? "BreastCancer_RF_v1",
                InputSummary = "Breast cancer prediction based on mean features.",
                InputDataJson = JsonSerializer.Serialize(request),
                OutputDataJson = JsonSerializer.Serialize(response),

                ResultLabel = response.Label,
                Probability = response.Confidence,

                Notes = Input.Notes
            };

            _db.Predictions.Add(prediction);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Doctor/Predictions/Details", new { id = prediction.Id });
        }

        private async Task LoadPatientsAsync(Guid? selectedId)
        {
            var patients = await _db.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName ?? p.User.Email)
                .ToListAsync();

            Patients = patients
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(p.User.FullName)
                        ? p.User.Email
                        : p.User.FullName,
                    Selected = selectedId.HasValue && p.Id == selectedId.Value
                })
                .ToList();
        }
    }
}
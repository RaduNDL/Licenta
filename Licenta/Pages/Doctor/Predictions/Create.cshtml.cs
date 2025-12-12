using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public CreateModel(AppDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public class InputModel
        {
            public Guid PatientId { get; set; }
            public string ModelName { get; set; } = "SkinCancerHAM10000";
            public string InputSummary { get; set; } = string.Empty;
            public IFormFile? File { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SelectList Patients { get; set; } = default!;

        public async Task OnGetAsync(Guid? patientId)
        {
            await LoadPatientsAsync(patientId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadPatientsAsync(Input.PatientId);
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            string? savedPath = null;

            if (Input.File != null)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath, "prediction-inputs");
                Directory.CreateDirectory(uploadsRoot);

                var fileName = $"{Guid.NewGuid()}_{Input.File.FileName}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await Input.File.CopyToAsync(stream);
                }

                savedPath = "/prediction-inputs/" + fileName;
            }

            // TODO: here you will actually call the ML model (API / external process)
            var resultLabel = "Pending";
            double? prob = null;

            var prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                PatientId = Input.PatientId,
                DoctorId = doctor.Id,
                ModelName = Input.ModelName,
                InputSummary = Input.InputSummary,
                ResultLabel = resultLabel,
                Probability = prob,
                CreatedAtUtc = DateTime.UtcNow,
                AttachmentPath = savedPath,
                Notes = string.Empty
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

            Patients = new SelectList(
                patients,
                nameof(PatientProfile.Id),
                "User.FullName",
                selectedId
            );
        }
    }
}
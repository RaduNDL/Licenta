using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Assistant.Predictions
{
    [Authorize(Roles = "Assistant")]
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
            [Required] public Guid PatientId { get; set; }
            [Required] public Guid DoctorId { get; set; }
            public string ModelName { get; set; } = "SkinCancerHAM10000";
            public string InputSummary { get; set; } = string.Empty;
            public IFormFile? File { get; set; }
        }

        [BindProperty] public InputModel Input { get; set; } = new();

        public SelectList Patients { get; set; }
        public SelectList Doctors { get; set; }

        public async Task OnGetAsync(Guid? patientId)
        {
            await LoadAsync(patientId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadAsync(Input.PatientId);
                return Page();
            }

            var assistant = await _userManager.GetUserAsync(User);

            string? savedPath = null;
            if (Input.File != null)
            {
                var root = Path.Combine(_env.WebRootPath, "prediction-inputs");
                Directory.CreateDirectory(root);
                var fileName = $"{Guid.NewGuid()}_{Input.File.FileName}";
                var filePath = Path.Combine(root, fileName);
                using (var stream = System.IO.File.Create(filePath))
                    await Input.File.CopyToAsync(stream);
                savedPath = "/prediction-inputs/" + fileName;
            }

            var prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                PatientId = Input.PatientId,
                DoctorId = Input.DoctorId,
                ModelName = Input.ModelName,
                InputSummary = Input.InputSummary,
                AttachmentPath = savedPath,
                ResultLabel = "Pending",
                Probability = null,
                CreatedAtUtc = DateTime.UtcNow,
                Notes = string.Empty,
                RequestedByAssistantId = assistant.Id
            };
            _db.Predictions.Add(prediction);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Doctor/Predictions/Details", new { id = prediction.Id });
        }

        private async Task LoadAsync(Guid? selectedPatient)
        {
            var patients = await _db.Patients.Include(p => p.User)
                .OrderBy(p => p.User.FullName ?? p.User.Email).ToListAsync();
            Patients = new SelectList(patients, nameof(PatientProfile.Id), "User.FullName", selectedPatient);

            var doctors = await _db.Doctors.Include(d => d.User)
                .OrderBy(d => d.User.FullName ?? d.User.Email).ToListAsync();
            Doctors = new SelectList(doctors, nameof(DoctorProfile.Id), "User.FullName");
        }
    }
}

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

        public FromAttachmentModel(AppDbContext db, UserManager<ApplicationUser> userManager, IMlImagingClient ml, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _ml = ml;
            _env = env;
        }

        public MedicalAttachment? Attachment { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            // FIX: Include Patient si User pentru a afisa numele in interfata
            Attachment = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (Attachment == null) return NotFound();

            // Verificare securitate: medicul are acces la pacient?
            if (!string.IsNullOrWhiteSpace(user.ClinicId) && Attachment.Patient?.User?.ClinicId != user.ClinicId)
            {
                return Forbid();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id, string modelId, int imageSize, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null) return Forbid();

            // Re-incarcam atasamentul cu tot cu pacient
            var att = await _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (att == null) return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, att.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                ErrorMessage = "Physical file not found on server.";
                Attachment = att;
                return Page();
            }

            try
            {
                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, ct);
                using var ms = new MemoryStream(fileBytes);

                // Apel catre ML Server (Port 8002)
                var result = await _ml.PredictImagingAsync(
                    ms,
                    att.FileName ?? "image.jpg",
                    att.ContentType ?? "image/jpeg",
                    modelId,
                    imageSize,
                    ct);

                if (result == null) throw new Exception("ML service returned no data.");

                var prediction = new Prediction
                {
                    Id = Guid.NewGuid(),
                    PatientId = att.PatientId,
                    DoctorId = doctor.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModelName = "ISIC2019",
                    ResultLabel = result.Label,
                    Probability = result.EffectiveProbability,
                    Status = PredictionStatus.Draft,
                    InputDataJson = JsonSerializer.Serialize(new
                    {
                        attachment_id = att.Id,
                        file_name = att.FileName,
                        content_type = att.ContentType,
                        model_id = modelId,
                        image_size = imageSize
                    }),
                    OutputDataJson = JsonSerializer.Serialize(result)
                };

                _db.Predictions.Add(prediction);
                await _db.SaveChangesAsync(ct);

                return RedirectToPage("/Doctor/Predictions/Record", new { id = prediction.Id });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Analysis Failed: {ex.Message}";
                Attachment = att;
                return Page();
            }
        }
    }
}
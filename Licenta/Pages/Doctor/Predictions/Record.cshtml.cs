using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class RecordModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public RecordModel(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public string ResultLabel { get; set; } = "-";
        public string Confidence { get; set; } = "-";
        public string RiskLevel { get; set; } = "-";
        public string MedicalNote { get; set; } = "-";
        public string ErrorMessage { get; set; }
        public string ImageUrl { get; set; }
        public Dictionary<string, float> Probabilities { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var att = await _db.MedicalAttachments.FindAsync(id);
            if (att == null) return NotFound();

            ImageUrl = att.FilePath;

            var webRoot = _env.WebRootPath;
            var relativePath = att.FilePath.TrimStart('/', '\\');
            var physicalPath = Path.Combine(webRoot, relativePath);

            if (!System.IO.File.Exists(physicalPath))
            {
                ErrorMessage = "Image file not found on server disk.";
                return Page();
            }

            try
            {
                using (var client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                        var fileContent = new ByteArrayContent(fileBytes);
                        content.Add(fileContent, "file", att.FileName);

                        var response = await client.PostAsync("http://localhost:8002/api/imaging/predict", content);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var result = JsonSerializer.Deserialize<JsonElement>(json);

                            ResultLabel = result.GetProperty("label").GetString();
                            var bestProb = result.GetProperty("best_probability").GetDouble();
                            Confidence = bestProb.ToString("P1");

                            var extras = result.GetProperty("extras");
                            RiskLevel = extras.GetProperty("risk_level").GetString();
                            MedicalNote = extras.GetProperty("medical_note").GetString();

                            var probs = result.GetProperty("probabilities");
                            foreach (var prop in probs.EnumerateObject())
                            {
                                Probabilities.Add(prop.Name, (float)prop.Value.GetDouble());
                            }
                        }
                        else
                        {
                            ErrorMessage = $"Python API Error: {response.StatusCode}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Connection Error: Is the Python Server running? ({ex.Message})";
            }

            return Page();
        }
    }
}
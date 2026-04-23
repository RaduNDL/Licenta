using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Predictions
{
    [Authorize(Roles = "Patient")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<Prediction> Items { get; set; } = new();

        public async Task OnGetAsync(CancellationToken ct)
        {
            Items = await GetPredictionsForCurrentPatientAsync(ct);
        }

        public async Task<JsonResult> OnGetListAsync(CancellationToken ct)
        {
            var items = await GetPredictionsForCurrentPatientAsync(ct);

            var payload = items.Select(p => new PredictionListItemDto
            {
                Id = p.Id,
                ModelName = p.ModelName ?? "-",
                ResultLabel = string.IsNullOrWhiteSpace(p.ResultLabel) ? "-" : p.ResultLabel,
                Probability = p.Probability,
                CreatedAtUtc = p.CreatedAtUtc
            }).ToList();

            return new JsonResult(payload);
        }

        private async Task<List<Prediction>> GetPredictionsForCurrentPatientAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return new List<Prediction>();

            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

            if (patient == null)
                return new List<Prediction>();

            return await _db.Predictions
                .AsNoTracking()
                .Where(p => p.PatientId == patient.Id && p.Status == PredictionStatus.Accepted)
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync(ct);
        }

        public class PredictionListItemDto
        {
            public Guid Id { get; set; }
            public string ModelName { get; set; } = "-";
            public string ResultLabel { get; set; } = "-";
            public float? Probability { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }
    }
}
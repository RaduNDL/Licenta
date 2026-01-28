using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public Guid? PatientId { get; set; }
        public List<Prediction> Items { get; set; } = new();

        private Dictionary<Guid, MedicalAttachment> _attachments = new();

        public async Task OnGetAsync(Guid? patientId, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id, ct);

            if (doctor == null) return;

            PatientId = patientId;

            var query = _db.Predictions
                .Include(p => p.Patient).ThenInclude(p => p.User)
                .Where(p => p.DoctorId == doctor.Id);

            if (patientId.HasValue && patientId.Value != Guid.Empty)
                query = query.Where(p => p.PatientId == patientId.Value);

            Items = await query
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(100)
                .ToListAsync(ct);

            var attachmentIds = Items
                .Select(p => ExtractAttachmentId(p.InputDataJson))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (attachmentIds.Count == 0)
                return;

            _attachments = await _db.MedicalAttachments
                .AsNoTracking()
                .Where(a => attachmentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);
        }

        public string GetImageUrl(string? json)
        {
            var id = ExtractAttachmentId(json);
            if (id == null) return "/img/placeholder-medical.png";

            if (_attachments.TryGetValue(id.Value, out var att))
                return att.FilePath;

            return "/img/placeholder-medical.png";
        }

        private static Guid? ExtractAttachmentId(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("attachment_id", out var prop))
                    return prop.GetGuid();
            }
            catch { }

            return null;
        }
    }
}

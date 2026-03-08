using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services;
using Licenta.Services.Ml;
using Licenta.Services.Ml.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Attachments
{
    [Authorize(Roles = "Doctor")]
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMlImagingClient _ml;
        private readonly IWebHostEnvironment _env;
        private readonly MlServiceOptions _mlOpt;
        private readonly INotificationService _notifier;

        public ReviewModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IMlImagingClient ml,
            IWebHostEnvironment env,
            IOptions<MlServiceOptions> mlOpt,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _ml = ml;
            _env = env;
            _mlOpt = mlOpt.Value;
            _notifier = notifier;
        }

        public MedicalAttachment Item { get; set; } = default!;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public string? DoctorNotes { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var existsDoctor = await _db.Doctors
                .AsNoTracking()
                .AnyAsync(d => d.UserId == user.Id);

            if (!existsDoctor) return Forbid();

            var item = await _db.MedicalAttachments
                .Include(a => a.Patient!)
                .ThenInclude(p => p.User!)
                .FirstOrDefaultAsync(a => a.Id == id);


            if (item == null) return NotFound();

            var docClinic = (user.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(docClinic))
            {
                var patClinic = (item.Patient?.User?.ClinicId ?? "").Trim();
                if (patClinic != docClinic) return Forbid();
            }

            Item = item;
            Input.DoctorNotes = item.DoctorNotes;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id, string action)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null) return Forbid();

            var item = await _db.MedicalAttachments
                .Include("Patient")
                .Include("Patient.User")
                .FirstOrDefaultAsync(a => a.Id == id);

            if (item == null) return NotFound();

            var docClinic = (user.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(docClinic))
            {
                var patClinic = (item.Patient?.User?.ClinicId ?? "").Trim();
                if (patClinic != docClinic) return Forbid();
            }

            if (!ModelState.IsValid)
            {
                Item = item;
                return Page();
            }

            item.DoctorNotes = Input.DoctorNotes;
            item.DoctorId = doctor.Id;
            item.ValidatedByDoctorId = doctor.Id;
            item.ValidatedAtUtc = DateTime.UtcNow;

            var patientUser = item.Patient?.User;

            if (string.Equals(action, "validate", StringComparison.OrdinalIgnoreCase))
            {
                item.Status = AttachmentStatus.Validated;
                await _db.SaveChangesAsync();

                if (patientUser != null)
                {
                    await _notifier.NotifyAsync(
                        patientUser,
                        NotificationType.Document,
                        "Document Validated",
                        $"Dr. {user.FullName} has validated your uploaded document.",
                        actionUrl: $"/Patient/Attachments/Details?id={item.Id}",
                        actionText: "View Details",
                        relatedEntity: "MedicalAttachment",
                        relatedEntityId: item.Id.ToString(),
                        sendEmail: false
                    );
                }

                TempData["StatusMessage"] = "Document validated manually.";
                return RedirectToPage("/Doctor/Attachments/Inbox");
            }

            if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
            {
                item.Status = AttachmentStatus.Rejected;
                await _db.SaveChangesAsync();

                if (patientUser != null)
                {
                    await _notifier.NotifyAsync(
                        patientUser,
                        NotificationType.Document,
                        "Document Rejected",
                        $"Dr. {user.FullName} has rejected your uploaded document.",
                        actionUrl: $"/Patient/Attachments/Details?id={item.Id}",
                        actionText: "View Details",
                        relatedEntity: "MedicalAttachment",
                        relatedEntityId: item.Id.ToString(),
                        sendEmail: false
                    );
                }

                TempData["StatusMessage"] = "Document rejected.";
                return RedirectToPage("/Doctor/Attachments/Inbox");
            }

            if (string.Equals(action, "ai", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(item.ContentType) ||
                    !item.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["StatusMessage"] = "AI analysis is allowed only for image attachments.";
                    return RedirectToPage("/Doctor/Attachments/Inbox");
                }

                var rel = (item.FilePath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(rel) || !rel.StartsWith("/"))
                {
                    TempData["StatusMessage"] = "Invalid attachment path.";
                    return RedirectToPage("/Doctor/Attachments/Inbox");
                }

                var relFs = rel.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
                var candidate = Path.Combine(_env.WebRootPath, relFs);

                var rootFull = Path.GetFullPath(_env.WebRootPath);
                var fileFull = Path.GetFullPath(candidate);

                if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["StatusMessage"] = "Invalid attachment path.";
                    return RedirectToPage("/Doctor/Attachments/Inbox");
                }

                if (!System.IO.File.Exists(fileFull))
                {
                    TempData["StatusMessage"] = "Attachment file is missing on disk.";
                    return RedirectToPage("/Doctor/Attachments/Inbox");
                }

                await using var fs = new FileStream(fileFull, FileMode.Open, FileAccess.Read, FileShare.Read);

                var requireDomain = _mlOpt.RequireDomainGateImaging;
                var requireQuality = false;

                var result = await _ml.PredictImagingAsync(
                    fs,
                    item.FileName ?? "image",
                    item.ContentType ?? "application/octet-stream",
                    "cbis_ddsm_images:torch_cnn",
                    224,
                    requireQuality,
                    requireDomain,
                    HttpContext.RequestAborted);

                if (IsRejected(result))
                {
                    item.Status = AttachmentStatus.Rejected;
                    await _db.SaveChangesAsync();

                    if (patientUser != null)
                    {
                        await _notifier.NotifyAsync(
                            patientUser,
                            NotificationType.Document,
                            "Image Rejected by AI",
                            $"Your image was rejected due to quality or domain issues. Please upload a valid image.",
                            actionUrl: $"/Patient/Attachments/Details?id={item.Id}",
                            actionText: "View Details",
                            relatedEntity: "MedicalAttachment",
                            relatedEntityId: item.Id.ToString(),
                            sendEmail: false
                        );
                    }

                    TempData["StatusMessage"] = BuildRejectMessage(result!);
                    return RedirectToPage("/Doctor/Attachments/Inbox");
                }

                item.Status = AttachmentStatus.Validated;
                await _db.SaveChangesAsync();

                if (patientUser != null)
                {
                    await _notifier.NotifyAsync(
                        patientUser,
                        NotificationType.Document,
                        "Image Validated",
                        $"Dr. {user.FullName} has validated your image and it is now being analyzed.",
                        actionUrl: $"/Patient/Attachments/Details?id={item.Id}",
                        actionText: "View Details",
                        relatedEntity: "MedicalAttachment",
                        relatedEntityId: item.Id.ToString(),
                        sendEmail: false
                    );
                }

                var prediction = new Prediction
                {
                    Id = Guid.NewGuid(),
                    PatientId = item.PatientId,
                    DoctorId = doctor.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModelName = "CBIS-DDSM",
                    InputSummary = item.FileName,
                    ResultLabel = string.IsNullOrWhiteSpace(result?.Label) ? "UNKNOWN" : result!.Label,
                    Probability = (float?)result?.EffectiveProbabilitySafe,
                    InputDataJson = JsonSerializer.Serialize(new
                    {
                        source = "patient_attachment",
                        attachment_id = item.Id
                    }),
                    OutputDataJson = JsonSerializer.Serialize(result),
                    Status = PredictionStatus.Draft
                };

                _db.Predictions.Add(prediction);
                await _db.SaveChangesAsync();

                return RedirectToPage("/Doctor/Predictions/Record", new { id = prediction.Id });
            }

            Item = item;
            return Page();
        }

        private static bool IsRejected(ImagingPredictResponse? r)
        {
            if (r == null) return true;

            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "OUT_OF_DOMAIN", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "UNUSABLE_IMAGE", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string BuildRejectMessage(ImagingPredictResponse r)
        {
            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "UNUSABLE_IMAGE", StringComparison.OrdinalIgnoreCase))
            {
                var issues = (r.QualityIssues != null && r.QualityIssues.Count > 0)
                    ? string.Join(", ", r.QualityIssues)
                    : "unknown_quality_issue";

                return $"AI rejected: image quality is not acceptable ({issues}).";
            }

            if (!string.IsNullOrWhiteSpace(r.Label) &&
                string.Equals(r.Label, "OUT_OF_DOMAIN", StringComparison.OrdinalIgnoreCase))
            {
                var issues = (r.DomainIssues != null && r.DomainIssues.Count > 0)
                    ? string.Join(", ", r.DomainIssues)
                    : "out_of_domain";

                return $"AI rejected: uploaded image does not look like a breast mammogram ({issues}).";
            }

            return "AI rejected: uploaded image is not acceptable.";
        }
    }
}
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Appointments
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

        public class RequestRowVm
        {
            public Guid AttachmentId { get; set; }
            public DateTime UploadedAtUtc { get; set; }
            public AttachmentStatus Status { get; set; }
            public DateTime? ValidatedAtUtc { get; set; }

            public string DoctorName { get; set; } = "Pending assignment";
            public string RequestedSlotDisplay { get; set; } = "-";
            public string SuggestedSlotDisplay { get; set; } = "-";
            public string ScheduledSlotDisplay { get; set; } = "-";
            public string NotesDisplay { get; set; } = "-";
            public string Reason { get; set; } = "-";

            public string StateLabel { get; set; } = "Pending review";
            public string StateClass { get; set; } = "status-pending";
            public string IconClass { get; set; } = "fa-clock";
        }

        public class AppointmentRowVm
        {
            public int AppointmentId { get; set; }
            public DateTime ScheduledAtUtc { get; set; }
            public AppointmentStatus Status { get; set; }
            public VisitStage VisitStage { get; set; }

            public string DoctorName { get; set; } = "-";
            public string Location { get; set; } = "-";
            public string Reason { get; set; } = "-";

            public string ProgressLabel { get; set; } = "Scheduled";
            public string ProgressIcon { get; set; } = "fa-calendar-check";
        }

        private sealed class ParsedNotes
        {
            public string Marker { get; set; } = "";
            public Dictionary<string, string> Parts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public List<RequestRowVm> Requests { get; set; } = new();
        public List<AppointmentRowVm> Appointments { get; set; } = new();

        public async Task OnGetAsync()
        {
            Requests = new();
            Appointments = new();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return;

            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
                return;

            var patientId = patient.Id;

            var reqs = await _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.Type == "AppointmentRequest" && a.PatientId == patientId)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            foreach (var a in reqs)
            {
                var doctorName = a.Doctor?.User?.FullName ?? a.Doctor?.User?.Email ?? "Pending assignment";

                var parsed = ParseValidationNotes(a.ValidationNotes);

                var requestedIso = ExtractRequestedIso(parsed, a.ValidationNotes);
                var suggestedIso = ExtractSuggestedIso(parsed, a.ValidationNotes);
                var scheduledIso = ExtractScheduledIso(parsed, a.ValidationNotes);

                var requestedDisplay = !string.IsNullOrWhiteSpace(requestedIso) ? FormatLocalIsoForDisplay(requestedIso!) : "-";
                var suggestedDisplay = !string.IsNullOrWhiteSpace(suggestedIso) ? FormatLocalIsoForDisplay(suggestedIso!) : "-";
                var scheduledDisplay = !string.IsNullOrWhiteSpace(scheduledIso) ? FormatLocalIsoForDisplay(scheduledIso!) : "-";

                var awaitingDoctor = IsAwaitingDoctorApproval(parsed, a.ValidationNotes);

                var (label, css, icon) = MapRequestState(a.Status, awaitingDoctor, parsed, a.ValidationNotes);
                var notes = BuildNotesDisplay(a.Status, awaitingDoctor, parsed, a.ValidationNotes, scheduledDisplay);

                Requests.Add(new RequestRowVm
                {
                    AttachmentId = a.Id,
                    UploadedAtUtc = a.UploadedAt,
                    Status = a.Status,
                    ValidatedAtUtc = a.ValidatedAtUtc,
                    DoctorName = doctorName,
                    RequestedSlotDisplay = requestedDisplay,
                    SuggestedSlotDisplay = suggestedDisplay,
                    ScheduledSlotDisplay = scheduledDisplay,
                    NotesDisplay = notes,
                    Reason = string.IsNullOrWhiteSpace(a.PatientNotes) ? "-" : a.PatientNotes!,
                    StateLabel = label,
                    StateClass = css,
                    IconClass = icon
                });
            }

            var appts = await _db.Appointments
                .AsNoTracking()
                .Include(x => x.Doctor).ThenInclude(d => d.User)
                .Where(x => x.PatientId == patientId)
                .OrderByDescending(x => x.ScheduledAt)
                .Select(x => new AppointmentRowVm
                {
                    AppointmentId = x.Id,
                    ScheduledAtUtc = x.ScheduledAt,
                    Status = x.Status,
                    VisitStage = x.VisitStage,
                    DoctorName = x.Doctor != null ? (x.Doctor.User.FullName ?? x.Doctor.User.Email) : "-",
                    Location = string.IsNullOrWhiteSpace(x.Location) ? "-" : x.Location,
                    Reason = string.IsNullOrWhiteSpace(x.Reason) ? "-" : x.Reason,
                    ProgressLabel = "Scheduled",
                    ProgressIcon = "fa-calendar-check"
                })
                .ToListAsync();

            foreach (var vm in appts)
            {
                var (pLabel, pIcon) = MapProgress(vm.VisitStage, vm.Status);
                vm.ProgressLabel = pLabel;
                vm.ProgressIcon = pIcon;
            }

            Appointments = appts;
        }

        private static (string label, string icon) MapProgress(VisitStage stage, AppointmentStatus status)
        {
            if (status == AppointmentStatus.Cancelled)
                return ("Cancelled", "fa-circle-xmark");

            if (status == AppointmentStatus.NoShow)
                return ("No-show", "fa-user-slash");

            if (status == AppointmentStatus.Completed)
                return ("Completed", "fa-circle-check");

            return stage switch
            {
                VisitStage.NotArrived => ("Scheduled", "fa-calendar-check"),
                VisitStage.CheckedIn => ("Checked in", "fa-clipboard-check"),
                VisitStage.InTriage => ("Triage", "fa-stethoscope"),
                VisitStage.WaitingDoctor => ("Waiting doctor", "fa-hourglass-half"),
                VisitStage.InConsultation => ("In consultation", "fa-user-doctor"),
                VisitStage.Finished => ("Finished", "fa-circle-check"),
                _ => ("Scheduled", "fa-calendar-check")
            };
        }

        private static (string label, string css, string icon) MapRequestState(AttachmentStatus status, bool awaitingDoctor, ParsedNotes parsed, string? rawNotes)
        {
            if (status == AttachmentStatus.Rejected)
                return ("Rejected", "status-rejected", "fa-circle-xmark");

            if (status == AttachmentStatus.Validated)
            {
                if (HasMarker(parsed, rawNotes, "SCHEDULED_BY_ASSISTANT"))
                    return ("Scheduled", "status-accepted", "fa-circle-check");

                if (HasMarker(parsed, rawNotes, "APPROVED_BY_DOCTOR"))
                    return ("Approved", "status-accepted", "fa-circle-check");

                if (HasMarker(parsed, rawNotes, "REJECTED_BY_DOCTOR"))
                    return ("Rejected", "status-rejected", "fa-circle-xmark");

                if (HasMarker(parsed, rawNotes, "REJECTED_BY_ASSISTANT"))
                    return ("Rejected", "status-rejected", "fa-circle-xmark");

                return ("Processed", "status-accepted", "fa-circle-check");
            }

            if (awaitingDoctor)
                return ("Awaiting doctor approval", "status-pending", "fa-user-doctor");

            return ("Pending review", "status-pending", "fa-clock");
        }

        private static string BuildNotesDisplay(AttachmentStatus status, bool awaitingDoctor, ParsedNotes parsed, string? rawNotes, string scheduledDisplay)
        {
            if (status == AttachmentStatus.Validated)
            {
                if (HasMarker(parsed, rawNotes, "SCHEDULED_BY_ASSISTANT"))
                {
                    if (!string.IsNullOrWhiteSpace(scheduledDisplay) && scheduledDisplay != "-")
                        return $"Scheduled by clinic staff for {scheduledDisplay}.";
                    return "Scheduled by clinic staff.";
                }

                if (HasMarker(parsed, rawNotes, "APPROVED_BY_DOCTOR"))
                {
                    if (!string.IsNullOrWhiteSpace(scheduledDisplay) && scheduledDisplay != "-")
                        return $"Approved by doctor for {scheduledDisplay}.";
                    return "Approved by doctor.";
                }

                if (HasMarker(parsed, rawNotes, "REJECTED_BY_DOCTOR"))
                    return "Rejected by doctor.";

                if (HasMarker(parsed, rawNotes, "REJECTED_BY_ASSISTANT"))
                    return "Rejected by clinic.";

                return "Processed by clinic.";
            }

            if (status == AttachmentStatus.Rejected)
            {
                if (HasMarker(parsed, rawNotes, "REJECTED_BY_DOCTOR"))
                    return "Rejected by doctor.";

                if (HasMarker(parsed, rawNotes, "REJECTED_BY_ASSISTANT"))
                    return "Rejected by clinic.";

                return "Rejected.";
            }

            if (awaitingDoctor)
                return "Forwarded to doctor for approval.";

            return "Your request is waiting for clinic review.";
        }

        private static bool IsAwaitingDoctorApproval(ParsedNotes parsed, string? rawNotes)
        {
            return HasMarker(parsed, rawNotes, "AWAITING_DOCTOR_APPROVAL");
        }

        private static bool HasMarker(ParsedNotes parsed, string? rawNotes, string marker)
        {
            if (!string.IsNullOrWhiteSpace(parsed.Marker) && parsed.Marker.Equals(marker, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(rawNotes) && rawNotes.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static string? ExtractRequestedIso(ParsedNotes parsed, string? rawNotes)
        {
            if (parsed.Parts.TryGetValue("Selected", out var v1) && !string.IsNullOrWhiteSpace(v1))
                return v1.Trim();

            if (string.IsNullOrWhiteSpace(rawNotes))
                return null;

            var idx = rawNotes.IndexOf("Selected:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var v = rawNotes[(idx + "Selected:".Length)..].Trim();
            if (v.Contains("|"))
                v = v.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        private static string? ExtractSuggestedIso(ParsedNotes parsed, string? rawNotes)
        {
            if (parsed.Parts.TryGetValue("Suggested", out var v1) && !string.IsNullOrWhiteSpace(v1))
                return v1.Trim();

            if (string.IsNullOrWhiteSpace(rawNotes))
                return null;

            var idx = rawNotes.IndexOf("Suggested:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var v = rawNotes[(idx + "Suggested:".Length)..].Trim();
            if (v.Contains("|"))
                v = v.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        private static string? ExtractScheduledIso(ParsedNotes parsed, string? rawNotes)
        {
            if (parsed.Parts.TryGetValue("Scheduled", out var v1) && !string.IsNullOrWhiteSpace(v1))
                return v1.Trim();

            if (string.IsNullOrWhiteSpace(rawNotes))
                return null;

            var idx = rawNotes.IndexOf("Scheduled:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var v = rawNotes[(idx + "Scheduled:".Length)..].Trim();
            if (v.Contains("|"))
                v = v.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        private static ParsedNotes ParseValidationNotes(string? notes)
        {
            var parsed = new ParsedNotes();

            if (string.IsNullOrWhiteSpace(notes))
                return parsed;

            var raw = notes.Trim();

            var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (parts.Count == 0)
                return parsed;

            var first = parts[0];

            if (first.Contains(':'))
            {
                var kv = SplitKeyValue(first);
                if (kv.key.Length > 0 && kv.value.Length > 0)
                    parsed.Parts[kv.key] = kv.value;
            }
            else
            {
                parsed.Marker = first.Trim();
            }

            for (int i = 1; i < parts.Count; i++)
            {
                var p = parts[i];
                if (!p.Contains(':'))
                    continue;

                var (k, v) = SplitKeyValue(p);
                if (k.Length == 0 || v.Length == 0)
                    continue;

                parsed.Parts[k] = v;
            }

            return parsed;
        }

        private static (string key, string value) SplitKeyValue(string s)
        {
            var idx = s.IndexOf(':');
            if (idx <= 0)
                return ("", "");

            var k = s[..idx].Trim();
            var v = s[(idx + 1)..].Trim();
            return (k, v);
        }

        private static string FormatLocalIsoForDisplay(string localIso)
        {
            var formats = new[]
            {
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss"
            };

            if (!DateTime.TryParseExact(localIso, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return localIso;

            var local = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return local.ToString("ddd dd MMM HH:mm");
        }
    }
}

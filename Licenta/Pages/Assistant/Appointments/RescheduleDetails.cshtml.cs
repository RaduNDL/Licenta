using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Appointments
{
    [Authorize(Roles = "Assistant")]
    public class RescheduleDetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public RescheduleDetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public int RequestId { get; set; }

        [BindProperty, Required]
        public DateTime? InputStartLocal { get; set; }

        [BindProperty, Required]
        public DateTime? InputEndLocal { get; set; }

        [BindProperty, Required, StringLength(120)]
        public string InputLocation { get; set; } = "Clinic";

        public bool CanView { get; set; }
        public bool CanPropose { get; set; }
        public bool CanFinalizeProposed { get; set; }

        public string Status { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string OldTime { get; set; } = "";
        public string Reason { get; set; } = "";
        public string? ProposeError { get; set; }

        public List<OptionVm> Options { get; set; } = new();

        public class OptionVm
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public string Location { get; set; } = "";
            public bool IsChosen { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAddOptionAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId);

            if (req == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                if (req.Patient?.User?.ClinicId != assistant.ClinicId)
                    return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync();
                return Page();
            }

            var startLocal = DateTime.SpecifyKind(InputStartLocal!.Value, DateTimeKind.Local);
            var endLocal = DateTime.SpecifyKind(InputEndLocal!.Value, DateTimeKind.Local);

            if (endLocal <= startLocal || startLocal <= DateTime.Now)
            {
                ProposeError = "Invalid time range.";
                await LoadAsync();
                return Page();
            }

            var startUtc = startLocal.ToUniversalTime();
            var endUtc = endLocal.ToUniversalTime();

            var conflict = await _db.Appointments
                .AnyAsync(a =>
                    a.DoctorId == req.DoctorId &&
                    a.Id != req.AppointmentId &&
                    a.Status != AppointmentStatus.Cancelled &&
                    a.Status != AppointmentStatus.Completed &&
                    a.StartTimeUtc < endUtc &&
                    startUtc < a.StartTimeUtc.AddMinutes(30));

            if (conflict)
            {
                ProposeError = "This option conflicts with another appointment.";
                await LoadAsync();
                return Page();
            }

            _db.Add(new AppointmentRescheduleOption
            {
                RescheduleRequestId = req.Id,
                ProposedStartUtc = startUtc,
                ProposedEndUtc = endUtc,
                Location = InputLocation.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });

            if (req.Status == AppointmentRescheduleStatus.Requested)
                req.Status = AppointmentRescheduleStatus.Proposed;

            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Option added.";
            return RedirectToPage(new { requestId = RequestId });
        }

        public async Task<IActionResult> OnPostFinalizeProposedAsync()
        {
            var assistant = await _userManager.GetUserAsync(User);
            if (assistant == null)
                return Unauthorized();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId);

            if (req == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(assistant.ClinicId))
            {
                if (req.Patient?.User?.ClinicId != assistant.ClinicId)
                    return Forbid();
            }

            var anyOptions = await _db.Set<AppointmentRescheduleOption>()
                .AnyAsync(o => o.RescheduleRequestId == req.Id);

            if (!anyOptions)
            {
                TempData["StatusMessage"] = "Add at least one option first.";
                return RedirectToPage(new { requestId = RequestId });
            }

            req.Status = AppointmentRescheduleStatus.Proposed;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Proposal sent to patient.";
            return RedirectToPage(new { requestId = RequestId });
        }

        private async Task LoadAsync()
        {
            CanView = false;
            Options.Clear();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId);

            if (req == null)
                return;

            CanView = true;

            Status = req.Status.ToString();
            PatientName = req.Patient?.User?.FullName ?? req.Patient?.User?.Email ?? "";
            DoctorName = req.Doctor?.User?.FullName ?? req.Doctor?.User?.Email ?? "";
            OldTime = req.OldScheduledAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Reason = req.Reason;

            CanPropose = req.Status == AppointmentRescheduleStatus.Requested || req.Status == AppointmentRescheduleStatus.Proposed;
            CanFinalizeProposed = CanPropose;

            Options = await _db.Set<AppointmentRescheduleOption>()
                .Where(o => o.RescheduleRequestId == req.Id)
                .OrderBy(o => o.ProposedStartUtc)
                .Select(o => new OptionVm
                {
                    Id = o.Id,
                    Display = $"{o.ProposedStartUtc.ToLocalTime():yyyy-MM-dd HH:mm} - {o.ProposedEndUtc.ToLocalTime():HH:mm}",
                    Location = o.Location,
                    IsChosen = o.IsChosen
                })
                .ToListAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PatientProfileEntity = Licenta.Models.PatientProfile;

namespace Licenta.Pages.Patient.Appointments
{
    [Authorize(Roles = "Patient")]
    public class ReschedulePickModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReschedulePickModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public int RequestId { get; set; }

        [BindProperty]
        [Required]
        public int? SelectedOptionId { get; set; }

        public bool CanView { get; set; }

        public string DoctorName { get; set; } = "Doctor";
        public string Status { get; set; } = "";
        public string OldTime { get; set; } = "";
        public string NewTime { get; set; } = "";
        public string DoctorDecisionNote { get; set; } = "";

        public List<OptionVm> Options { get; set; } = new();

        public class OptionVm
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public string Location { get; set; } = "Clinic";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var patient = await _db.Set<PatientProfileEntity>().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null) return Forbid();

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.PatientId == patient.Id);

            if (req == null) return NotFound();

            if (req.Status != AppointmentRescheduleStatus.Proposed)
            {
                TempData["StatusMessage"] = "You cannot select an option for this request.";
                return RedirectToPage(new { requestId = RequestId });
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync();
                return Page();
            }

            var opt = await _db.Set<AppointmentRescheduleOption>()
                .FirstOrDefaultAsync(o => o.Id == SelectedOptionId!.Value && o.RescheduleRequestId == req.Id);

            if (opt == null)
            {
                TempData["StatusMessage"] = "Invalid option.";
                return RedirectToPage(new { requestId = RequestId });
            }

            await _db.Set<AppointmentRescheduleOption>()
                .Where(o => o.RescheduleRequestId == req.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsChosen, false));

            opt.IsChosen = true;

            req.SelectedOptionId = opt.Id;
            req.Status = AppointmentRescheduleStatus.PatientSelected;
            req.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Selection submitted. Waiting for doctor approval.";
            return RedirectToPage(new { requestId = RequestId });
        }

        private async Task LoadAsync()
        {
            CanView = false;
            Options = new();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var patient = await _db.Set<PatientProfileEntity>().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (patient == null) return;

            var req = await _db.Set<AppointmentRescheduleRequest>()
                .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == RequestId && r.PatientId == patient.Id);

            if (req == null) return;

            CanView = true;

            DoctorName = req.Appointment?.Doctor?.User?.FullName ?? req.Appointment?.Doctor?.User?.Email ?? "Doctor";
            Status = req.Status.ToString();
            OldTime = req.OldScheduledAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            DoctorDecisionNote = req.DoctorDecisionNote ?? "";

            if (req.NewScheduledAtUtc.HasValue)
                NewTime = req.NewScheduledAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            if (req.Status == AppointmentRescheduleStatus.Proposed)
            {
                var opts = await _db.Set<AppointmentRescheduleOption>()
                    .Where(o => o.RescheduleRequestId == req.Id)
                    .OrderBy(o => o.ProposedStartUtc)
                    .ToListAsync();

                Options = opts.Select(o => new OptionVm
                {
                    Id = o.Id,
                    Display = $"{o.ProposedStartUtc.ToLocalTime():yyyy-MM-dd HH:mm} - {o.ProposedEndUtc.ToLocalTime():HH:mm}",
                    Location = string.IsNullOrWhiteSpace(o.Location) ? "Clinic" : o.Location
                }).ToList();
            }
        }
    }
}

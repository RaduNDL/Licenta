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

namespace Licenta.Pages.Doctor.Appointments
{
    [Authorize(Roles = "Doctor")]
    public class ApprovalsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApprovalsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<MedicalAttachment> Items { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
            if (doctor == null)
                return Forbid();

            var doctorId = doctor.Id;
            var clinicId = user.ClinicId;

            var q = _db.MedicalAttachments
                .AsNoTracking()
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Where(a => a.Type == "AppointmentRequest"
                            && a.Status == AttachmentStatus.Pending
                            && a.DoctorId == doctorId
                            && a.ValidationNotes != null
                            && EF.Functions.Like(a.ValidationNotes, "%AWAITING_DOCTOR_APPROVAL%"));

            if (!string.IsNullOrWhiteSpace(clinicId))
                q = q.Where(a => a.Patient != null && a.Patient.User != null && a.Patient.User.ClinicId == clinicId);

            Items = await q
                .OrderByDescending(a => a.UploadedAt)
                .Take(300)
                .ToListAsync(ct);

            return Page();
        }
    }
}

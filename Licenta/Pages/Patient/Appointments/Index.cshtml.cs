using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

        public class RowVm
        {
            public string FileName { get; set; } = "";
            public string FilePath { get; set; } = "";
            public string Type { get; set; } = "";
            public DateTime UploadedAt { get; set; }
            public AttachmentStatus Status { get; set; }
            public DateTime? ValidatedAtUtc { get; set; }
            public string? ValidationNotes { get; set; }
            public string? DoctorUserFullName { get; set; }
        }

        public List<RowVm> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                Items = new();
                return;
            }

            var query = _db.MedicalAttachments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.Patient != null &&
                            a.Patient.UserId == user.Id &&
                            a.Type == "AppointmentRequest")
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new RowVm
                {
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    Type = a.Type,
                    UploadedAt = a.UploadedAt,
                    Status = a.Status,
                    ValidatedAtUtc = a.ValidatedAtUtc,
                    ValidationNotes = a.ValidationNotes,
                    DoctorUserFullName = a.Doctor != null ? (a.Doctor.User.FullName ?? a.Doctor.User.Email) : null
                });

            Items = await query.ToListAsync();
        }
    }
}

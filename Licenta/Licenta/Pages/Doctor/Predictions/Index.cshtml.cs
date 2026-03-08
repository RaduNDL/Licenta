using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
            .AsNoTracking()
            .Include(p => p.Patient!)
            .ThenInclude(pat => pat.User!)
            .Where(p => p.DoctorId == doctor.Id);

            if (patientId.HasValue && patientId.Value != Guid.Empty)
                query = query.Where(p => p.PatientId == patientId.Value);

            Items = await query
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(100)
                .ToListAsync(ct);
        }
    }
}

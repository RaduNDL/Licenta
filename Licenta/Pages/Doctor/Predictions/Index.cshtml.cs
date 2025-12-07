using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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

        public async Task OnGetAsync(Guid? patientId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                Items = new();
                return;
            }

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                Items = new();
                return;
            }

            PatientId = patientId;

            var query = _db.Predictions
                .Include(p => p.Patient).ThenInclude(pp => pp.User)
                .Where(p => p.DoctorId == doctor.Id);

            if (patientId.HasValue)
            {
                var pid = patientId.Value;
                query = query.Where(p => p.PatientId == pid);
            }

            Items = await query
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync();
        }
    }
}

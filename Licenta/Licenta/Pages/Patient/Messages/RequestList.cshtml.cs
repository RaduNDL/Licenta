using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Messages
{
    [Authorize(Roles = "Patient")]
    public class RequestListModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequestListModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IList<PatientMessageRequest> Requests { get; set; } = new List<PatientMessageRequest>();
         
        public async Task OnGetAsync()
        {
            var patientUser = await _userManager.GetUserAsync(User);
            if (patientUser == null) return;

            var patientProfileId = await _db.Patients
                .Where(p => p.UserId == patientUser.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

            if (patientProfileId == null)
            {
                Requests = new List<PatientMessageRequest>();
                return;
            }

            Requests = await _db.PatientMessageRequests
                .Include(r => r.Assistant)
                .Include(r => r.DoctorProfile).ThenInclude(d => d.User)
                .Where(r => r.PatientId == patientProfileId.Value)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
            var patient = await _userManager.GetUserAsync(User);

            Requests = await _db.PatientMessageRequests
                .Include(r => r.Doctor)
                .Where(r => r.PatientId == patient.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}

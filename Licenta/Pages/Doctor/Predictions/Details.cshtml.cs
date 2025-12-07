using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Pages.Doctor.Predictions
{
    [Authorize(Roles = "Doctor")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public Prediction Item { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            Item = await _db.Predictions
                .Include(p => p.Patient).ThenInclude(pp => pp.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Item == null || Item.DoctorId != doctor.Id)
                return NotFound();

            return Page();
        }
    }
}

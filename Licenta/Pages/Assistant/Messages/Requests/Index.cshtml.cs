using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Licenta.Pages.Assistant.Messages.Requests
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public IList<PatientMessageRequest> Requests { get; set; } = new List<PatientMessageRequest>();

        public string? StatusFilter { get; set; }

        public async Task OnGetAsync(string? status)
        {
            StatusFilter = status;

            var query = _db.PatientMessageRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<PatientMessageRequestStatus>(status, out var statusEnum))
            {
                query = query.Where(r => r.Status == statusEnum);
            }

            Requests = await query.ToListAsync();
        }
    }
}

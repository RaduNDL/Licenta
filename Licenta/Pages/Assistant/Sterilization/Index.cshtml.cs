using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Pages.Assistant.Sterilization
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<SterilizationCycle> Cycles { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [Display(Name = "Device name")]
            public string DeviceName { get; set; } = "Autoclave 1";

            [Required]
            [Display(Name = "Date / time")]
            public DateTime PerformedAt { get; set; } = DateTime.Now;

            [Required]
            [Display(Name = "Cycle number")]
            public string CycleNumber { get; set; } = string.Empty;

            [Display(Name = "Notes")]
            public string? Notes { get; set; }
        }

        public async Task OnGetAsync()
        {
            Cycles = await _db.SterilizationCycles
                .OrderByDescending(c => c.PerformedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!ModelState.IsValid)
            {
                Cycles = await _db.SterilizationCycles
                    .OrderByDescending(c => c.PerformedAt)
                    .Take(50)
                    .ToListAsync();
                return Page();
            }

            var entity = new SterilizationCycle
            {
                DeviceName = Input.DeviceName.Trim(),
                PerformedAt = DateTime.SpecifyKind(Input.PerformedAt, DateTimeKind.Local)
                                .ToUniversalTime(),
                CycleNumber = Input.CycleNumber.Trim(),
                Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim()
            };

            _db.SterilizationCycles.Add(entity);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Sterilization cycle registered.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var entity = await _db.SterilizationCycles.FindAsync(id);
            if (entity == null)
            {
                TempData["StatusMessage"] = "Entry not found.";
                return RedirectToPage();
            }

            _db.SterilizationCycles.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Entry deleted.";
            return RedirectToPage();
        }
    }
}

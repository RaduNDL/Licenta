using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Pages.Assistant.Inventory
{
    [Authorize(Roles = "Assistant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<InventoryItem> Items { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public int? Id { get; set; }

            [Required]
            [Display(Name = "Name")]
            public string Name { get; set; } = string.Empty;

            [Display(Name = "Category")]
            public string? Category { get; set; }

            [Display(Name = "Quantity")]
            [Range(0, int.MaxValue)]
            public int Quantity { get; set; }

            [Display(Name = "Min quantity")]
            [Range(0, int.MaxValue)]
            public int MinQuantity { get; set; }

            [Display(Name = "Expiry date")]
            [DataType(DataType.Date)]
            public DateTime? ExpiryDate { get; set; }
        }

        public async Task OnGetAsync()
        {
            Items = await _db.InventoryItems
                .OrderBy(i => i.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddOrUpdateAsync()
        {
            if (!ModelState.IsValid)
            {
                Items = await _db.InventoryItems
                    .OrderBy(i => i.Name)
                    .ToListAsync();
                return Page();
            }

            InventoryItem entity;

            if (Input.Id.HasValue)
            {
                entity = await _db.InventoryItems.FindAsync(Input.Id.Value)
                         ?? new InventoryItem();
            }
            else
            {
                entity = new InventoryItem();
                _db.InventoryItems.Add(entity);
            }

            entity.Name = Input.Name.Trim();
            entity.Category = string.IsNullOrWhiteSpace(Input.Category)
                ? null
                : Input.Category.Trim();
            entity.Quantity = Input.Quantity;
            entity.MinQuantity = Input.MinQuantity;
            entity.ExpiryDate = Input.ExpiryDate;

            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Inventory item saved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(int id, int quantity)
        {
            var item = await _db.InventoryItems.FindAsync(id);
            if (item == null)
            {
                TempData["StatusMessage"] = "Item not found.";
                return RedirectToPage();
            }

            if (quantity < 0)
            {
                TempData["StatusMessage"] = "Quantity cannot be negative.";
                return RedirectToPage();
            }

            item.Quantity = quantity;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Quantity updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var item = await _db.InventoryItems.FindAsync(id);
            if (item == null)
            {
                TempData["StatusMessage"] = "Item not found.";
                return RedirectToPage();
            }

            _db.InventoryItems.Remove(item);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Item deleted.";
            return RedirectToPage();
        }
    }
}

using System;

namespace Licenta.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }

        public int Quantity { get; set; }
        public int MinQuantity { get; set; } = 0;

        public DateTime? ExpiryDate { get; set; }
    }

}
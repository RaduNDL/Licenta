using System;

namespace Licenta.Models
{
    public class SterilizationCycle
    {
        public int Id { get; set; }

        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        public string DeviceName { get; set; } = "Autoclave 1";
        public string CycleNumber { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
}

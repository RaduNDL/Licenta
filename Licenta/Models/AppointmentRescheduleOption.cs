using System;

namespace Licenta.Models
{
    public class AppointmentRescheduleOption
    {
        public int Id { get; set; }

        public int RescheduleRequestId { get; set; }
        public AppointmentRescheduleRequest RescheduleRequest { get; set; } = null!;

        public DateTime ProposedStartUtc { get; set; }
        public DateTime ProposedEndUtc { get; set; }

        public string Location { get; set; } = "Clinic";

        public bool IsChosen { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

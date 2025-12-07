using System;

namespace Licenta.Models
{
    public class MlIntakeForm
    {
        public int Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public string Symptoms { get; set; } = string.Empty;

        public decimal? Temperature { get; set; }
        public int? HeartRate { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public decimal? OxygenSaturation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }
    }
}

using System;
using Licenta.Areas.Identity.Data;
namespace Licenta.Models
{
    public class MlIntakeForm
    {
        public int Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public decimal? Temperature { get; set; }
        public decimal? OxygenSaturation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class IntakeFormViewModel
    {
        [Required]
        public int PatientId { get; set; }

        public int? AppointmentId { get; set; }

        [Required]
        public string Symptoms { get; set; } = null!;

        public decimal? Temperature { get; set; }
        public int? HeartRate { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public decimal? OxygenSaturation { get; set; }
    }

}

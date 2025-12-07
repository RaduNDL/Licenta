using System;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class DoctorAvailability
    {
        public int Id { get; set; }

        [Required]
        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }
        [Required]
        public TimeSpan EndTime { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

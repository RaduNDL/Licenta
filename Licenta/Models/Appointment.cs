using System;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;
        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        [DataType(DataType.DateTime)]
        public DateTime ScheduledAt { get; set; }

        public string? Reason { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        public string? CancelReason { get; set; }
        public string? RescheduleReason { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}

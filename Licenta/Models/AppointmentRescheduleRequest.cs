using System;

namespace Licenta.Models
{
    public class AppointmentRescheduleRequest
    {
        public int Id { get; set; }

        public int AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public Guid DoctorId { get; set; }
        public DoctorProfile Doctor { get; set; } = null!;

        public AppointmentRescheduleStatus Status { get; set; } = AppointmentRescheduleStatus.Requested;

        public string Reason { get; set; } = string.Empty;
        public string PreferredWindows { get; set; } = string.Empty;

        public DateTime OldScheduledAtUtc { get; set; }
        public DateTime? NewScheduledAtUtc { get; set; }

        public int? SelectedOptionId { get; set; }
        public AppointmentRescheduleOption? SelectedOption { get; set; }

        public string? DoctorDecisionNote { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
    }
}

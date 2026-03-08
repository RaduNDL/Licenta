using System;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public enum PatientMessageRequestStatus
    {
        Pending = 0,
        AssistantChat = 1,
        WaitingDoctorApproval = 2,
        ActiveDoctorChat = 3,
        RejectedByDoctor = 4,
        Closed = 5
    }

    public class PatientMessageRequest
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }
        public PatientProfile Patient { get; set; } = null!;

        public string? AssistantId { get; set; }
        public ApplicationUser? Assistant { get; set; }

        public Guid DoctorProfileId { get; set; }
        public DoctorProfile DoctorProfile { get; set; } = null!;

        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        public PatientMessageRequestStatus Status { get; set; } = PatientMessageRequestStatus.Pending;

        public string? AssistantNote { get; set; }
        public string? EscalationReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
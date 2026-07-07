using System;
using System.ComponentModel.DataAnnotations;
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
        Closed = 5,
        RejectedByAssistant = 6
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

        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;

        public PatientMessageRequestStatus Status { get; set; } = PatientMessageRequestStatus.Pending;

        [MaxLength(2000)]
        public string? AssistantNote { get; set; }

        [MaxLength(2000)]
        public string? EscalationReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
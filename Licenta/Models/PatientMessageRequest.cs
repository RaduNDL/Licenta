using System;

namespace Licenta.Models
{
    public enum PatientMessageRequestStatus { Pending = 0, Approved = 1, Rejected = 2 }

    public class PatientMessageRequest
    {
        public Guid Id { get; set; }

        public string? PatientId { get; set; }
        public ApplicationUser? Patient { get; set; }

        public string? DoctorId { get; set; }
        public ApplicationUser? Doctor { get; set; }

        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public PatientMessageRequestStatus Status { get; set; } = PatientMessageRequestStatus.Pending;

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedByAdminId { get; set; }
        public ApplicationUser? ReviewedByAdmin { get; set; }

        public string? AdminNote { get; set; }
    }
}

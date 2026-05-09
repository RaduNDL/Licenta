using System;
using System.ComponentModel.DataAnnotations;
using Licenta.Areas.Identity.Data;

namespace Licenta.Models
{
    public class InternalMessage
    {
        public Guid Id { get; set; }

        public string SenderId { get; set; } = null!;
        public ApplicationUser Sender { get; set; } = null!;

        public string RecipientId { get; set; } = null!;
        public ApplicationUser Recipient { get; set; } = null!;

        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
    }
}
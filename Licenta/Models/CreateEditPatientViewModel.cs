using System;
using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class CreateEditPatientViewModel
    {
        public Guid? Id { get; set; }  

        [Required]
        public string FirstName { get; set; } = null!;
        [Required]
        public string LastName { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? NationalId { get; set; }

        [DataType(DataType.Password)]
        public string? InitialPassword { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Licenta.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string ClinicName { get; set; } = "";

        public int MaxUploadMb { get; set; } = 10;

        [MaxLength(500)]
        public string? LogoPath { get; set; }

        public int PasswordMinLength { get; set; } = 6;

        public bool RequireDigit { get; set; }
        public bool RequireUppercase { get; set; }
        public bool RequireSpecialChar { get; set; }

        public bool IdentitySeeded { get; internal set; }
    }
}
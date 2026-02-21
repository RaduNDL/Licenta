namespace Licenta.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }
        public string ClinicName { get; set; } = "LicentaMed Clinic";
        public string? LogoPath { get; set; }
        public int MaxUploadMb { get; set; } = 20;
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public bool SmtpUseSSL { get; set; } = true;
        public int PasswordMinLength { get; set; } = 6;
        public bool RequireDigit { get; set; } = true;
        public bool RequireUppercase { get; set; } = false;
        public bool RequireSpecialChar { get; set; } = false;
    }
}

using System.Threading.Tasks;
using Licenta.Models;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Areas.Identity.Data
{
    public static class SystemSettingsSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            await db.Database.MigrateAsync();

            if (!await db.SystemSettings.AnyAsync())
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    ClinicName = "LicentaMed Clinic",
                    MaxUploadMb = 20,

                    SmtpServer = "smtp.example.com",
                    SmtpPort = 587,
                    SmtpUseSSL = true,

                    PasswordMinLength = 6,
                    RequireDigit = true,
                    RequireUppercase = false,
                    RequireSpecialChar = false
                });

                await db.SaveChangesAsync();
            }
        }
    }
}

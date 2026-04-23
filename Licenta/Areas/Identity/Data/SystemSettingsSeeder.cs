using Licenta.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Licenta.Areas.Identity.Data
{
    public static class SystemSettingsSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            if (!await db.SystemSettings.AnyAsync())
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    ClinicName = "LicentaMed Clinic",
                    MaxUploadMb = 20,
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
using System;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Licenta.Data
{
    public static class IdentitySeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var settings = await db.SystemSettings.FirstOrDefaultAsync();
            if (settings != null && settings.IdentitySeeded)
                return;

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roles = { "Administrator", "Doctor", "Assistant", "Patient" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!result.Succeeded)
                        throw new Exception($"Failed to create role '{role}': " +
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            var adminEmail = config["SeedAccounts:AdminEmail"] ?? "admin@gmail.com";
            var adminPassword = config["SeedAccounts:AdminPassword"] ?? throw new InvalidOperationException("SeedAccounts:AdminPassword is not configured.");
            var doctorEmail = config["SeedAccounts:DoctorEmail"] ?? "doctor@gmail.com";
            var doctorPassword = config["SeedAccounts:DoctorPassword"] ?? throw new InvalidOperationException("SeedAccounts:DoctorPassword is not configured.");
            var assistantEmail = config["SeedAccounts:AssistantEmail"] ?? "assistant@gmail.com";
            var assistantPassword = config["SeedAccounts:AssistantPassword"] ?? throw new InvalidOperationException("SeedAccounts:AssistantPassword is not configured.");
            var patientEmail = config["SeedAccounts:PatientEmail"] ?? "patient@gmail.com";
            var patientPassword = config["SeedAccounts:PatientPassword"] ?? throw new InvalidOperationException("SeedAccounts:PatientPassword is not configured.");

            var sharedClinicId = await EnsureSharedClinicIdAsync(userManager, assistantEmail);

            var admin = await EnsureUserInRole(userManager, adminEmail, adminPassword,
                "System Administrator", "Administrator", sharedClinicId);

            var doctorUser = await EnsureUserInRole(userManager, doctorEmail, doctorPassword,
                "Default Doctor", "Doctor", sharedClinicId);

            var assistantUser = await EnsureUserInRole(userManager, assistantEmail, assistantPassword,
                "Default Assistant", "Assistant", sharedClinicId);

            var patientUser = await EnsureUserInRole(userManager, patientEmail, patientPassword,
                "Default Patient", "Patient", sharedClinicId);

            await EnsureDoctorProfileAsync(db, doctorUser);
            await EnsurePatientProfileAsync(db, patientUser);

            if (settings != null)
            {
                settings.IdentitySeeded = true;
                await db.SaveChangesAsync();
            }
            else
            {
                await db.SaveChangesAsync();
            }
        }
        private static async Task<string> EnsureSharedClinicIdAsync(UserManager<ApplicationUser> userManager, string assistantEmail)
        {
            var assistant = await userManager.FindByEmailAsync(assistantEmail);
            var cid = (assistant?.ClinicId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(cid))
                return cid;

            return GenerateClinicId();
        }

        private static async Task<ApplicationUser> EnsureUserInRole(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string fullName,
            string role,
            string clinicId)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    ClinicId = clinicId
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                    throw new Exception($"Failed creating default user '{email}': " +
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
            else
            {
                var needUpdate = false;

                if (user.EmailConfirmed != true)
                {
                    user.EmailConfirmed = true;
                    needUpdate = true;
                }

                if ((user.FullName ?? "").Trim() != fullName.Trim())
                {
                    user.FullName = fullName;
                    needUpdate = true;
                }

                if ((user.ClinicId ?? "").Trim() != clinicId.Trim())
                {
                    user.ClinicId = clinicId;
                    needUpdate = true;
                }

                if (needUpdate)
                {
                    var updateResult = await userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                        throw new Exception($"Failed updating default user '{email}': " +
                            string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, role);
                if (!addRoleResult.Succeeded)
                    throw new Exception($"Failed assigning role '{role}' to '{email}': " +
                        string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }

            return user;
        }

        private static string GenerateClinicId()
        {
            var guid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            return $"CL-{guid}";
        }

        private static async Task EnsureDoctorProfileAsync(AppDbContext db, ApplicationUser doctorUser)
        {
            var existing = await db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);
            if (existing != null) return;

            db.Doctors.Add(new DoctorProfile
            {
                Id = Guid.NewGuid(),
                UserId = doctorUser.Id
            });
        }

        private static async Task EnsurePatientProfileAsync(AppDbContext db, ApplicationUser patientUser)
        {
            var existing = await db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUser.Id);
            if (existing != null) return;

            db.Patients.Add(new PatientProfile
            {
                Id = Guid.NewGuid(),
                UserId = patientUser.Id
            });
        }
    }
}
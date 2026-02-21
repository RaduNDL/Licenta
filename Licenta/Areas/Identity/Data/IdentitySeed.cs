using System;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Licenta.Data
{
    public static class IdentitySeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

            var sharedClinicId = await EnsureSharedClinicIdAsync(userManager);

            var admin = await EnsureUserInRole(
                userManager,
                email: "admin@gmail.com",
                password: "Parola123!",
                fullName: "System Administrator",
                role: "Administrator",
                clinicId: sharedClinicId);

            var doctorUser = await EnsureUserInRole(
                userManager,
                email: "doctor@gmail.com",
                password: "Parola123!",
                fullName: "Default Doctor",
                role: "Doctor",
                clinicId: sharedClinicId);

            var assistantUser = await EnsureUserInRole(
                userManager,
                email: "assistant@gmail.com",
                password: "Parola123!",
                fullName: "Default Assistant",
                role: "Assistant",
                clinicId: sharedClinicId);

            var patientUser = await EnsureUserInRole(
                userManager,
                email: "patient@gmail.com",
                password: "Parola123!",
                fullName: "Default Patient",
                role: "Patient",
                clinicId: sharedClinicId);

            await EnsureDoctorProfileAsync(db, doctorUser);
            await EnsurePatientProfileAsync(db, patientUser);

            await db.SaveChangesAsync();
        }

        private static async Task<string> EnsureSharedClinicIdAsync(UserManager<ApplicationUser> userManager)
        {
            var assistant = await userManager.FindByEmailAsync("assistant@gmail.com");
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

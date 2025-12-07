using System;
using System.Linq;
using System.Threading.Tasks;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
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
                    {
                        throw new Exception(
                            $"Failed to create role '{role}': " +
                            string.Join(", ", result.Errors.Select(e => e.Description))
                        );
                    }
                }
            }

            var admin = await EnsureUserInRole(
                userManager,
                email: "admin@gmail.com",
                password: "Parola123!",
                fullName: "System Administrator",
                role: "Administrator");

            var doctorUser = await EnsureUserInRole(
                userManager,
                email: "doctor@gmail.com",
                password: "Parola123!",
                fullName: "Default Doctor",
                role: "Doctor");

            var assistantUser = await EnsureUserInRole(
                userManager,
                email: "assistant@gmail.com",
                password: "Parola123!",
                fullName: "Default Assistant",
                role: "Assistant");

            var patientUser = await EnsureUserInRole(
                userManager,
                email: "patient@gmail.com",
                password: "Parola123!",
                fullName: "Default Patient",
                role: "Patient");

            await EnsureDoctorProfileAsync(db, doctorUser);
            await EnsurePatientProfileAsync(db, patientUser);

            await db.SaveChangesAsync();
        }

        private static async Task<ApplicationUser> EnsureUserInRole(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string fullName,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    throw new Exception($"Failed creating default user '{email}': " +
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, role);
                if (!addRoleResult.Succeeded)
                {
                    throw new Exception($"Failed assigning role '{role}' to '{email}': " +
                        string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                }
            }

            return user;
        }

        private static async Task EnsureDoctorProfileAsync(AppDbContext db, ApplicationUser doctorUser)
        {
            var existing = await db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);
            if (existing != null)
                return;

            var profile = new DoctorProfile
            {
                Id = Guid.NewGuid(),
                UserId = doctorUser.Id
            };

            db.Doctors.Add(profile);
        }

        private static async Task EnsurePatientProfileAsync(AppDbContext db, ApplicationUser patientUser)
        {
            var existing = await db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUser.Id);
            if (existing != null)
                return;

            var profile = new PatientProfile
            {
                Id = Guid.NewGuid(),
                UserId = patientUser.Id
            };

            db.Patients.Add(profile);
        }
    }
}

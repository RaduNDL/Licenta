using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Licenta.Infrastructure.Security
{
    public class DynamicPasswordValidator : IPasswordValidator<ApplicationUser>
    {
        private readonly AppDbContext _db;

        public DynamicPasswordValidator(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        {
            password ??= "";

            var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var minLen = s?.PasswordMinLength ?? 6;
            var requireDigit = s?.RequireDigit ?? true;
            var requireUpper = s?.RequireUppercase ?? false;
            var requireSpecial = s?.RequireSpecialChar ?? false;

            var errors = new List<IdentityError>();

            if (password.Length < minLen)
                errors.Add(new IdentityError { Code = "PasswordTooShort", Description = $"Password must be at least {minLen} characters long." });

            if (requireDigit && !password.Any(char.IsDigit))
                errors.Add(new IdentityError { Code = "PasswordRequiresDigit", Description = "Password must contain at least one digit." });

            if (requireUpper && !password.Any(char.IsUpper))
                errors.Add(new IdentityError { Code = "PasswordRequiresUpper", Description = "Password must contain at least one uppercase letter." });

            if (requireSpecial && !Regex.IsMatch(password, @"[\W_]"))
                errors.Add(new IdentityError { Code = "PasswordRequiresSpecial", Description = "Password must contain at least one special character." });

            if (errors.Count == 0)
                return IdentityResult.Success;

            return IdentityResult.Failed(errors.ToArray());
        }
    }
}

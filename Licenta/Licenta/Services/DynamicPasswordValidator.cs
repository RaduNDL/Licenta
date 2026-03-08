using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Licenta.Services
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

            var s = await _db.SystemSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (s == null)
                return IdentityResult.Success;

            var errors = new List<IdentityError>();

            var minLen = s.PasswordMinLength < 4 ? 4 : s.PasswordMinLength;

            if (password.Length < minLen)
                errors.Add(new IdentityError { Code = "PasswordTooShort", Description = $"Password must be at least {minLen} characters long." });

            if (s.RequireDigit && !password.Any(char.IsDigit))
                errors.Add(new IdentityError { Code = "PasswordRequiresDigit", Description = "Password must contain at least one digit." });

            if (s.RequireUppercase && !password.Any(char.IsUpper))
                errors.Add(new IdentityError { Code = "PasswordRequiresUpper", Description = "Password must contain at least one uppercase letter." });

            if (s.RequireSpecialChar && !Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
                errors.Add(new IdentityError { Code = "PasswordRequiresNonAlphanumeric", Description = "Password must contain at least one special character." });

            return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
        }
    }
}
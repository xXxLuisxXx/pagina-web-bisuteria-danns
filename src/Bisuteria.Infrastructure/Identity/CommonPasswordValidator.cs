using Microsoft.AspNetCore.Identity;

namespace Bisuteria.Infrastructure.Identity;

public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var common = new[] { "password", "contraseña", "contrasena", "qwerty", "123456789", "admin123" };
        var weak = password is null || common.Any(p => password.Contains(p, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(weak ? IdentityResult.Failed(new IdentityError { Code = "CommonPassword", Description = "Elige una contraseña menos predecible." }) : IdentityResult.Success);
    }
}

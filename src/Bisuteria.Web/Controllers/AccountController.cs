using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Models;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Bisuteria.Web.Controllers;

public partial class AccountController : Controller
{
    [HttpGet]
    public IActionResult ConfirmEmail(string email, string token)
        => View(new EmailConfirmationViewModel { Email = email, Token = token });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmail(EmailConfirmationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        try
        {
            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
            if (user is { IsActive: true } && (await _userManager.ConfirmEmailAsync(user, token)).Succeeded)
            {
                TempData["Success"] = "Correo confirmado. Ya puedes iniciar sesión.";
                return RedirectToAction(nameof(Login));
            }
        }
        catch (FormatException) { }
        ModelState.AddModelError("", "El enlace de confirmación no es válido.");
        return View(model);
    }

    [Authorize, HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (IsInitialPassword(user, model.Password) || await _userManager.CheckPasswordAsync(user, model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "La nueva contraseña debe ser diferente de la actual.");
            return View(model);
        }
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "No se pudo cambiar la contraseña. Revisa la contraseña actual y usa una nueva de 12 caracteres con mayúscula, minúscula, número y símbolo.");
            return View(model);
        }
        user.MustChangePassword = false;
        if (!(await _userManager.UpdateAsync(user)).Succeeded) return StatusCode(503);
        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Contraseña actualizada. Las otras sesiones quedaron invalidadas.";
        return RedirectToAction(nameof(Profile));
    }

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICatalogService _catalog;
    private readonly Bisuteria.Web.Services.SecurityAuditLogger _audit;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICatalogService catalog,
        Bisuteria.Web.Services.SecurityAuditLogger audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _catalog = catalog;
        _audit = audit;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool reauthenticationRequired = false)
    {
        ViewData["Title"] = "Iniciar sesión";
        ViewBag.ReauthenticationRequired = reauthenticationRequired;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["Title"] = "Iniciar sesión";
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Ejecuta el hash de Identity también cuando el correo no existe para
            // reducir la diferencia temporal entre cuentas registradas y ajenas.
            _userManager.PasswordHasher.HashPassword(new ApplicationUser(), model.Password);
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(model);
        }

        var alreadyLocked = await _userManager.IsLockedOutAsync(user);
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (result.IsLockedOut && !alreadyLocked)
            await ApplyProgressiveLockoutAsync(user);
        if (result.Succeeded && user.IsActive)
        {
            var login = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (login.RequiresTwoFactor)
                return RedirectToAction(nameof(TwoFactor), new { returnUrl = model.ReturnUrl, rememberMe = model.RememberMe });
            if (!login.Succeeded) return RedirectToAction(nameof(Login));
            await ResetLockoutProgressAsync(user);
            await _audit.PrivilegedLoginAsync(user);
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        HttpContext.RequestServices.GetRequiredService<ILogger<AccountController>>()
            .LogWarning("Intento de acceso rechazado a las {Timestamp}", DateTimeOffset.UtcNow);
        ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["Title"] = "Crear cuenta";
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        ViewData["Title"] = "Crear cuenta";
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            PhoneNumber = model.Phone,
            EmailConfirmed = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "No se pudo completar el registro. Contacta con administración.");
                return View(model);
            }
            await HttpContext.RequestServices.GetRequiredService<PasswordRecoveryQueue>().EnqueueAsync(user.Email!, confirm: true);
            if (_userManager.Options.SignIn.RequireConfirmedEmail)
            {
                TempData["Success"] = "Revisa tu correo para confirmar la cuenta antes de iniciar sesión.";
                return RedirectToAction(nameof(Login));
            }
            await _signInManager.SignInAsync(user, isPersistent: false);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        foreach (var error in result.Errors)
        {
            var key = error.Code switch
            {
                "DuplicateEmail" or "DuplicateUserName" => nameof(model.Email),
                "PasswordTooShort" or "PasswordRequiresDigit" or "PasswordRequiresUpper" or
                    "PasswordRequiresLower" or "PasswordRequiresNonAlphanumeric" or "CommonPassword" => nameof(model.Password),
                _ => string.Empty
            };
            ModelState.AddModelError(key, GetIdentityErrorMessage(error.Code));
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Recuperar contraseña";
        return View(new ForgotPasswordViewModel());
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["Title"] = "Recuperar contraseña";
        if (!ModelState.IsValid)
            return View(model);

        await HttpContext.RequestServices.GetRequiredService<PasswordRecoveryQueue>().EnqueueAsync(model.Email);

        TempData["Success"] = "Si el correo está registrado, recibirás instrucciones para restablecer tu contraseña.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return RedirectToAction(nameof(Login));

        ViewData["Title"] = "Restablecer contraseña";
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewData["Title"] = "Restablecer contraseña";
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "No fue posible restablecer la contraseña. Solicita un nuevo enlace.");
            return View(model);
        }

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token)); }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "El enlace de recuperación no es válido.");
            return View(model);
        }

        if (IsInitialPassword(user, model.Password) || await _userManager.CheckPasswordAsync(user, model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "La nueva contraseña debe ser diferente de la actual.");
            return View(model);
        }
        var result = await _userManager.ResetPasswordAsync(user, token, model.Password);
        if (result.Succeeded)
        {
            user.MustChangePassword = false;
            if (!(await _userManager.UpdateAsync(user)).Succeeded) return StatusCode(503);
            TempData["Success"] = "Tu contraseña se actualizó correctamente. Ya puedes iniciar sesión.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, GetIdentityErrorMessage(error.Code));
        return View(model);
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        ViewData["Title"] = "Mi perfil";
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));

        var model = new ProfileViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber,
            City = user.City,
            Province = user.Province
        };

        ViewBag.Favorites = await _catalog.GetUserFavoritesAsync(user.Id, ct);
        return View(model);
    }

    [Authorize]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        ViewData["Title"] = "Mi perfil";
        var currentId = _userManager.GetUserId(User);
        ViewBag.Favorites = await _catalog.GetUserFavoritesAsync(currentId!);
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = model.Phone;
        user.City = model.City;
        user.Province = model.Province;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Tu perfil se actualizó correctamente.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, GetIdentityErrorMessage(error.Code));

        return View(model);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Acceso denegado";
        return View();
    }

    private bool IsInitialPassword(ApplicationUser user, string password)
        => user.InitialPasswordHash is { Length: > 0 } hash && _userManager.PasswordHasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;

    private async Task ApplyProgressiveLockoutAsync(ApplicationUser user)
    {
        var strikes = (await _userManager.GetClaimsAsync(user)).FirstOrDefault(claim => claim.Type == "security:lockout-level");
        var level = strikes is not null && int.TryParse(strikes.Value, out var previous) ? Math.Min(previous + 1, 5) : 0;
        var replacement = new System.Security.Claims.Claim("security:lockout-level", level.ToString());
        if (strikes is null) await _userManager.AddClaimAsync(user, replacement);
        else await _userManager.ReplaceClaimAsync(user, strikes, replacement);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(Math.Min(60, 5 * Math.Pow(2, level))));
    }

    private async Task ResetLockoutProgressAsync(ApplicationUser user)
    {
        var strike = (await _userManager.GetClaimsAsync(user)).FirstOrDefault(claim => claim.Type == "security:lockout-level");
        if (strike is not null) await _userManager.RemoveClaimAsync(user, strike);
    }

    private static string GetIdentityErrorMessage(string code) => code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => "Este correo ya está registrado.",
        "PasswordTooShort" => "La contraseña debe tener al menos 12 caracteres.",
        "PasswordRequiresDigit" => "La contraseña debe incluir un número.",
        "PasswordRequiresUpper" => "La contraseña debe incluir una letra mayúscula.",
        "PasswordRequiresLower" => "La contraseña debe incluir una letra minúscula.",
        "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir un símbolo.",
        "CommonPassword" => "Elige una contraseña menos predecible y que no contenga tu correo.",
        _ => "No fue posible completar la operación. Verifica los datos e inténtalo nuevamente."
    };
}

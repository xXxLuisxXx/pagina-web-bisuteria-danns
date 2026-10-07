using Bisuteria.Application.Interfaces;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Models;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Bisuteria.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICatalogService _catalog;
    private readonly IEmailSender _emailSender;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICatalogService catalog,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _catalog = catalog;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Title"] = "Iniciar sesión";
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
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tu cuenta se encuentra desactivada. Contáctanos para más información.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Demasiados intentos fallidos. Intenta nuevamente en unos minutos.");
            return View(model);
        }

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
            await _signInManager.SignInAsync(user, isPersistent: false);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, GetIdentityErrorMessage(error.Code));

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

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is { IsActive: true })
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var publicUrl = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["PublicBaseUrl"];
            var resetPath = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token = encodedToken });
            var resetUrl = Uri.TryCreate(publicUrl, UriKind.Absolute, out var origin) && origin.Scheme == "https"
                ? new Uri(origin, resetPath).AbsoluteUri : null;
            if (resetUrl is null)
                HttpContext.RequestServices.GetRequiredService<ILogger<AccountController>>().LogError("Falta configurar PublicBaseUrl con el origen HTTPS de la tienda.");
            if (!string.IsNullOrWhiteSpace(resetUrl))
            {
                try
                {
                    await _emailSender.SendPasswordResetAsync(user.Email!, resetUrl);
                }
                catch (Exception ex)
                {
                    // La respuesta no revela si la cuenta existe ni detalles de infraestructura.
                    HttpContext.RequestServices.GetRequiredService<ILogger<AccountController>>()
                        .LogError(ex, "No se pudo enviar el correo de recuperación.");
                }
            }
        }

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

        var result = await _userManager.ResetPasswordAsync(user, token, model.Password);
        if (result.Succeeded)
        {
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

    private static string GetIdentityErrorMessage(string code) => code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => "Este correo ya está registrado.",
        "PasswordTooShort" => "La contraseña debe tener al menos 8 caracteres.",
        "PasswordRequiresDigit" => "La contraseña debe incluir un número.",
        "PasswordRequiresUpper" => "La contraseña debe incluir una letra mayúscula.",
        "PasswordRequiresLower" => "La contraseña debe incluir una letra minúscula.",
        _ => "No fue posible completar la operación. Verifica los datos e inténtalo nuevamente."
    };
}

using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bisuteria.Web.Controllers;

public partial class AccountController
{
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> TwoFactor(string? returnUrl = null, bool rememberMe = false)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is not { IsActive: true }) return RedirectToAction(nameof(Login));
        return View(new TwoFactorViewModel { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("public-forms")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> TwoFactor(TwoFactorViewModel model)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is not { IsActive: true }) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(model);
        if (await _userManager.IsLockedOutAsync(user)) return RedirectToAction(nameof(Login));
        var result = model.Recovery
            ? await _signInManager.TwoFactorRecoveryCodeSignInAsync(model.Code.Trim())
            : await _signInManager.TwoFactorAuthenticatorSignInAsync(model.Code.Trim(), model.RememberMe, rememberClient: false);
        if (result.Succeeded)
            return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Home");
        // Identity contabiliza fallos TOTP; los códigos de recuperación requieren contabilización explícita.
        if (model.Recovery) await _userManager.AccessFailedAsync(user);
        ModelState.AddModelError("", "No se pudo verificar el acceso. Revisa el código o vuelve a iniciar sesión.");
        return View(model);
    }

    [Authorize, HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Authenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is not { IsActive: true }) return Challenge();
        return View(new AuthenticatorViewModel { Enabled = await _userManager.GetTwoFactorEnabledAsync(user) });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("public-forms")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Authenticator(AuthenticatorViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is not { IsActive: true }) return Challenge();
        model.Enabled = await _userManager.GetTwoFactorEnabledAsync(user);
        if (!ModelState.IsValid) return View(model);
        var password = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        ModelState.Remove(nameof(model.Password));
        model.Password = "";
        if (!password.Succeeded)
        {
            ModelState.AddModelError("", "No se pudo verificar tu identidad.");
            return View(model);
        }
        if (model.Enabled) return View(model);
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            if (!(await _userManager.ResetAuthenticatorKeyAsync(user)).Succeeded) return StatusCode(503);
            await _signInManager.RefreshSignInAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }
        model.SharedKey = key;
        if (string.IsNullOrEmpty(model.Code)) return View(model);
        if (!await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, model.Code))
        {
            await _userManager.AccessFailedAsync(user);
            ModelState.AddModelError(nameof(model.Code), "El código no es válido.");
            return View(model);
        }
        if (!(await _userManager.SetTwoFactorEnabledAsync(user, true)).Succeeded) return StatusCode(503);
        model.RecoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        if (model.RecoveryCodes is null) return StatusCode(503);
        if (!(await _userManager.UpdateSecurityStampAsync(user)).Succeeded) return StatusCode(503);
        await _signInManager.RefreshSignInAsync(user);
        model.Enabled = true;
        model.SharedKey = null;
        return View(model);
    }
}

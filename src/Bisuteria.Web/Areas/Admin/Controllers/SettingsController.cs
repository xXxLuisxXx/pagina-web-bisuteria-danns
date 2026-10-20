using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.StoreSettingsEdit)]
public class SettingsController : AdminBaseController
{
    private readonly ISettingService _settings;

    public SettingsController(ISettingService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Configuración";
        var settings = await _settings.GetAllAsync(ct);
        settings.TryAdd("MonthlySalesGoal", "0");
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Dictionary<string, string> values, CancellationToken ct)
    {
        if (values is not null)
        {
            var existing = await _settings.GetAllAsync(ct);
            existing.TryAdd("MonthlySalesGoal", "0");
            foreach (var kv in values)
            {
                var value = kv.Value ?? "";
                var invalid = !existing.ContainsKey(kv.Key) || value.Length > 1000;
                if (kv.Key == "CouponSingleUsePerCustomer") invalid |= !bool.TryParse(value, out _);
                if (kv.Key == "PaymentBankTransferEnabled") invalid |= !bool.TryParse(value, out _);
                if (kv.Key is "WhatsApp" or "ContactPhone" && value.Length > 0)
                    invalid |= !System.Text.RegularExpressions.Regex.IsMatch(value, @"^\+?[0-9 ()-]{7,25}$");
                if (kv.Key == "ContactEmail" && value.Length > 0)
                    invalid |= !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
                if (kv.Key.EndsWith("Url", StringComparison.Ordinal) && value.Length > 0)
                    invalid |= !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo);
                if (kv.Key is "ShippingCost" or "MonthlySalesGoal" || (kv.Key == "FreeShippingThreshold" && value.Length > 0))
                    invalid |= !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0 || amount > 1000000 || decimal.Round(amount, 2) != amount;
                if (invalid)
                {
                    ModelState.AddModelError($"values[{kv.Key}]", "Revisa el formato, importe o longitud de este campo.");
                }
            }
            if (values.TryGetValue("PaymentBankTransferEnabled", out var transferEnabled) &&
                bool.TryParse(transferEnabled, out var isTransferEnabled) && isTransferEnabled)
            {
                foreach (var key in new[] { "BankName", "BankAccountType", "BankAccountNumber", "BankAccountHolder" })
                    if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(key)) && string.IsNullOrWhiteSpace(existing.GetValueOrDefault(key)))
                        ModelState.AddModelError($"values[{key}]", "Completa este dato antes de habilitar la transferencia.");
            }
            if (!ModelState.IsValid)
            {
                foreach (var kv in values.Where(kv => existing.ContainsKey(kv.Key)))
                    existing[kv.Key] = kv.Value ?? "";
                ViewData["Title"] = "Configuración";
                return View("Index", existing);
            }
            foreach (var kv in values)
            {
                var group = kv.Key switch
                {
                    "StoreName" or "StoreSlogan" or "StoreDescription" => "General",
                    "WhatsApp" or "ContactEmail" or "ContactPhone" or "ContactAddress" => "Contacto",
                    "PaymentBankTransferEnabled" or "BankName" or "BankAccountType" or "BankAccountNumber" or "BankAccountHolder" or "BankAccountId" => "Pagos",
                    "FacebookUrl" or "InstagramUrl" or "TikTokUrl" => "Redes",
                    "ShippingCost" or "FreeShippingThreshold" => "Compras",
                    _ => "General"
                };
                await _settings.SetAsync(kv.Key, kv.Value, group, ct);
            }
        }

        TempData["Success"] = "Configuración guardada.";
        return RedirectToAction(nameof(Index));
    }
}

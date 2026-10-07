using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

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
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Dictionary<string, string> values, CancellationToken ct)
    {
        if (values is not null)
        {
            var existing = await _settings.GetAllAsync(ct);
            foreach (var kv in values)
            {
                var value = kv.Value ?? "";
                var invalid = !existing.ContainsKey(kv.Key) || value.Length > 1000;
                if (kv.Key is "WhatsApp" or "InstagramUrl" or "InstagramHandle" or "TikTokUrl" or "TikTokHandle")
                    invalid |= value != existing.GetValueOrDefault(kv.Key);
                if (kv.Key.EndsWith("Url", StringComparison.Ordinal) && value.Length > 0)
                    invalid |= !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo);
                if (kv.Key is "ShippingCost" or "FreeShippingThreshold")
                    invalid |= !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0 || amount > 1000000;
                if (invalid)
                {
                    TempData["Error"] = "Configuración inválida. Revisa las direcciones HTTPS, los importes y la longitud de los campos.";
                    return RedirectToAction(nameof(Index));
                }
            }
            foreach (var kv in values)
            {
                var group = kv.Key switch
                {
                    "StoreName" or "StoreSlogan" or "StoreDescription" => "General",
                    "WhatsApp" or "ContactEmail" or "ContactPhone" or "ContactAddress" => "Contacto",
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

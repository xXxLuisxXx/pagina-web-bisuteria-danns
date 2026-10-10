using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de configuración del sitio.
/// </summary>
public class SettingService : ISettingService
{
    private const string ShippingKey = "ShippingCost";
    private const string FreeShippingKey = "FreeShippingThreshold";

    private readonly IUnitOfWork _unitOfWork;
    private Dictionary<string, string>? _cache;

    public SettingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string?> GetAsync(string key, string? fallback = null, CancellationToken ct = default)
    {
        var setting = await _unitOfWork.Repository<SiteSetting>()
            .FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value ?? fallback;
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache is not null) return new Dictionary<string, string>(_cache);
        var settings = await _unitOfWork.Repository<SiteSetting>().ListAsync(ct);
        _cache = settings.ToDictionary(s => s.Key, s => s.Value ?? string.Empty);
        foreach (var key in _cache.Keys.Where(k => k.EndsWith("Url", StringComparison.Ordinal)).ToArray())
            if (!Uri.TryCreate(_cache[key], UriKind.Absolute, out var uri) || uri.Scheme != "https") _cache[key] = "";
        return new Dictionary<string, string>(_cache);
    }

    public async Task SetAsync(string key, string? value, string? group = null, CancellationToken ct = default)
    {
        _cache = null;
        var repo = _unitOfWork.Repository<SiteSetting>();
        var setting = await repo.FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting is null)
        {
            await repo.AddAsync(new SiteSetting { Key = key, Value = value, Group = group }, ct);
        }
        else
        {
            setting.Value = value;
            if (group is not null)
                setting.Group = group;
            repo.Update(setting);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetShippingCostAsync(CancellationToken ct = default)
    {
        var value = await GetAsync(ShippingKey, null, ct);
        return decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? Math.Max(0, parsed) : 0m;
    }

    public async Task<decimal?> GetFreeShippingThresholdAsync(CancellationToken ct = default)
    {
        var value = await GetAsync(FreeShippingKey, null, ct);
        return decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}

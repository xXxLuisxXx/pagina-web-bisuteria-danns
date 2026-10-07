namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Configuración del sitio (nombre de marca, eslogan, redes sociales, WhatsApp...).
/// </summary>
public interface ISettingService
{
    /// <summary>Devuelve el valor de una clave o un valor por defecto.</summary>
    Task<string?> GetAsync(string key, string? fallback = null, CancellationToken cancellationToken = default);

    /// <summary>Devuelve todos los valores como diccionario.</summary>
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Establece el valor de una clave (inserta o actualiza).</summary>
    Task SetAsync(string key, string? value, string? group = null, CancellationToken cancellationToken = default);

    /// <summary>Obtiene el costo de envío configurado.</summary>
    Task<decimal> GetShippingCostAsync(CancellationToken cancellationToken = default);

    /// <summary>Indica si el envío es gratis a partir del monto dado.</summary>
    Task<decimal?> GetFreeShippingThresholdAsync(CancellationToken cancellationToken = default);
}

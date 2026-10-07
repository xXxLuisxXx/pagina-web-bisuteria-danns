using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Configuración clave-valor del sitio (nombre de marca, eslogan, redes, WhatsApp, etc.).
/// </summary>
public class SiteSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    /// <summary>Grupo para agrupar la configuración en el panel.</summary>
    public string? Group { get; set; }
}

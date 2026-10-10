using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Personalización de una pieza creada mediante el Personalizador.
/// </summary>
public class Customization : BaseEntity
{
    public string? QuotaKey { get; set; }
    /// <summary>Producto base opcional sobre el que se personaliza.</summary>
    public int? ProductId { get; set; }

    public virtual Product? Product { get; set; }

    /// <summary>Texto libre (nombre, frase, etc.).</summary>
    public string? Text { get; set; }

    public string? Initials { get; set; }

    /// <summary>Fecha con significado para el cliente (guardada como texto para flexibilidad).</summary>
    public string? Date { get; set; }

    public string? Phrase { get; set; }

    public string? Color { get; set; }

    public string? Font { get; set; }

    public string? Charm { get; set; }

    public string? Material { get; set; }

    public string? Size { get; set; }

    public int Quantity { get; set; } = 1;

    public bool GiftWrap { get; set; }

    public string? Note { get; set; }

    /// <summary>Precio adicional que la personalización agrega a la pieza.</summary>
    public decimal PriceOverride { get; set; }

    public bool HasCustomization =>
        !string.IsNullOrWhiteSpace(Text) || !string.IsNullOrWhiteSpace(Initials) ||
        !string.IsNullOrWhiteSpace(Date) || !string.IsNullOrWhiteSpace(Phrase);

    public string BuildSummary()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Text)) parts.Add($"Texto: {Text}");
        if (!string.IsNullOrWhiteSpace(Initials)) parts.Add($"Iniciales: {Initials}");
        if (!string.IsNullOrWhiteSpace(Date)) parts.Add($"Fecha: {Date}");
        if (!string.IsNullOrWhiteSpace(Phrase)) parts.Add($"Frase: {Phrase}");
        if (!string.IsNullOrWhiteSpace(Color)) parts.Add($"Color: {Color}");
        if (!string.IsNullOrWhiteSpace(Font)) parts.Add($"Fuente: {Font}");
        if (!string.IsNullOrWhiteSpace(Charm)) parts.Add($"Dije: {Charm}");
        if (!string.IsNullOrWhiteSpace(Material)) parts.Add($"Material: {Material}");
        if (!string.IsNullOrWhiteSpace(Size)) parts.Add($"Tamaño: {Size}");
        if (GiftWrap) parts.Add("Empaque de regalo");
        if (!string.IsNullOrWhiteSpace(Note)) parts.Add($"Nota: {Note}");
        return string.Join(" • ", parts);
    }
}

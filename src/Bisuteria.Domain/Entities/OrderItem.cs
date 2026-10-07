using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Línea de detalle de un pedido.
/// </summary>
public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }

    public virtual Order? Order { get; set; }

    public int? ProductId { get; set; }

    public virtual Product? Product { get; set; }

    /// <summary>Nombre congelado al momento de la compra.</summary>
    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

    /// <summary>Personalización asociada (si el producto fue personalizado).</summary>
    public int? CustomizationId { get; set; }

    public virtual Customization? Customization { get; set; }

    /// <summary>Resumen legible de la personalización, p. ej. "Texto: María • Rosa • Script".</summary>
    public string? CustomizationSummary { get; set; }
}

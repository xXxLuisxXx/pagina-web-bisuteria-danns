namespace Bisuteria.Application.DTOs.Cart;

/// <summary>
/// Elemento del carrito de compras.
/// </summary>
/// <summary>
/// Datos de la personalización enviados por el Personalizador.
/// </summary>
public class CustomizationInputDto
{
    public int? ProductId { get; set; }

    public string? Text { get; set; }

    public string? Initials { get; set; }

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

    public decimal PriceOverride { get; set; }
}

public class CartItemDto
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? Slug { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public int Stock { get; set; }

    /// <summary>Id de la personalización creada para este artículo (si aplica).</summary>
    public int? CustomizationId { get; set; }

    /// <summary>Descripción de la personalización (para mostrar en el carrito).</summary>
    public string? CustomizationSummary { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public bool IsPersonalized => CustomizationId.HasValue;
}

/// <summary>
/// Estado completo del carrito incluyendo descuentos y envío.
/// </summary>
public class CartDto
{
    public List<CartItemDto> Items { get; set; } = new();

    public decimal Subtotal => Items.Sum(i => i.LineTotal);

    public decimal Discount { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal Total => Math.Max(0, Subtotal - Discount) + ShippingCost;

    public string? CouponCode { get; set; }

    public string? CouponMessage { get; set; }

    public int TotalItems => Items.Sum(i => i.Quantity);

    public bool IsEmpty => Items.Count == 0;
}

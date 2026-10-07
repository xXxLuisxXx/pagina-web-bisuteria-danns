using Bisuteria.Domain.Common;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Cupón o código promocional aplicable al carrito.
/// </summary>
public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public DiscountType Type { get; set; } = DiscountType.Percentage;

    /// <summary>Valor del descuento (porcentaje 0-100 o monto fijo).</summary>
    public decimal Value { get; set; }

    /// <summary>Monto mínimo de compra para poder usarse.</summary>
    public decimal MinAmount { get; set; }

    /// <summary>Máximo de usos totales (null = ilimitado).</summary>
    public int? MaxUses { get; set; }

    public int UsedCount { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    public bool IsValidFor(decimal orderAmount, DateTime now) =>
        IsActive &&
        (StartsAt is null || StartsAt <= now) &&
        (ExpiresAt is null || ExpiresAt >= now) &&
        (MaxUses is null || UsedCount < MaxUses) &&
        orderAmount >= MinAmount;
}

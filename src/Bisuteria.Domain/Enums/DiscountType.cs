namespace Bisuteria.Domain.Enums;

/// <summary>
/// Tipo de descuento aplicable por un cupón o promoción.
/// </summary>
public enum DiscountType
{
    /// <summary>Descuento porcentual sobre el subtotal.</summary>
    Percentage = 1,

    /// <summary>Descuento por monto fijo.</summary>
    FixedAmount = 2
}

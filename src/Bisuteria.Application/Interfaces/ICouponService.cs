using Bisuteria.Application.DTOs.Cart;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de cupones y descuentos del carrito.
/// </summary>
public interface ICouponService
{
    /// <summary>Valida un cupón y aplica el descuento al subtotal. Devuelve 0 si no aplica.</summary>
    Task<decimal> CalculateDiscountAsync(string code, decimal subtotal, CancellationToken cancellationToken = default);

    /// <summary>Valida un cupón y devuelve un mensaje amigable (para mostrar en el carrito).</summary>
    Task<CouponValidationResult> ValidateAsync(string code, decimal subtotal, CancellationToken cancellationToken = default);

    /// <summary>Incrementa el contador de usos del cupón.</summary>
    Task RegisterUseAsync(string code, CancellationToken cancellationToken = default);
}

public record CouponValidationResult(bool IsValid, decimal Discount, string Message);

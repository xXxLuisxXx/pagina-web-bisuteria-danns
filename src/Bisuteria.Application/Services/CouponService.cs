using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de cupones.
/// </summary>
public class CouponService : ICouponService
{
    private readonly IUnitOfWork _unitOfWork;

    public CouponService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<decimal> CalculateDiscountAsync(string code, decimal subtotal, CancellationToken ct = default)
    {
        var result = await ValidateAsync(code, subtotal, ct);
        return result.IsValid ? result.Discount : 0;
    }

    public async Task<CouponValidationResult> ValidateAsync(string code, decimal subtotal, CancellationToken ct = default)
    {
        var coupon = await _unitOfWork.Repository<Coupon>()
            .FirstOrDefaultAsync(c => c.Code.ToUpper() == code.Trim().ToUpper(), ct);

        if (coupon is null)
            return new CouponValidationResult(false, 0, "El código no es válido.");

        var now = DateTime.UtcNow;
        if (!coupon.IsActive)
            return new CouponValidationResult(false, 0, "Este cupón ya no está activo.");

        if (coupon.StartsAt.HasValue && coupon.StartsAt > now)
            return new CouponValidationResult(false, 0, "Este cupón aún no está disponible.");

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt < now)
            return new CouponValidationResult(false, 0, "Este cupón ha expirado.");

        if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses.Value)
            return new CouponValidationResult(false, 0, "Este cupón ha alcanzado su límite de usos.");

        if (subtotal < coupon.MinAmount)
            return new CouponValidationResult(false, 0,
                $"Este cupón aplica a partir de {coupon.MinAmount:C} de compra.");

        var discount = coupon.Type == DiscountType.Percentage
            ? subtotal * (coupon.Value / 100m)
            : coupon.Value;

        discount = decimal.Round(Math.Clamp(discount, 0, subtotal), 2, MidpointRounding.AwayFromZero);
        return new CouponValidationResult(true, discount, $"Descuento aplicado: {discount:C}");
    }

    public async Task RegisterUseAsync(string code, CancellationToken ct = default)
    {
        var coupon = await _unitOfWork.Repository<Coupon>()
            .FirstOrDefaultAsync(c => c.Code.ToUpper() == code.Trim().ToUpper(), ct);
        if (coupon is null)
            return;

        coupon.UsedCount++;
        _unitOfWork.Repository<Coupon>().Update(coupon);
        // El pedido confirma el cupón junto con inventario y pago en SaveChangesAsync.
    }
}

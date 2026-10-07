using Bisuteria.Application.Validators;
using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Servicio de administración de cupones de descuento.
/// </summary>
public class CouponAdminService : ICouponAdminService
{
    private readonly IUnitOfWork _unitOfWork;

    public CouponAdminService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<CouponDto>> GetListAsync(CancellationToken ct = default)
        => await _unitOfWork.Repository<Coupon>()
            .Query()
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Code)
            .Select(c => new CouponDto
            {
                Id = c.Id,
                Code = c.Code,
                Type = c.Type,
                Value = c.Value,
                MinAmount = c.MinAmount,
                MaxUses = c.MaxUses,
                UsedCount = c.UsedCount,
                StartsAt = c.StartsAt,
                ExpiresAt = c.ExpiresAt,
                IsActive = c.IsActive,
                Description = c.Description
            })
            .ToListAsync(ct);

    public async Task<CouponInputDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var coupon = await _unitOfWork.Repository<Coupon>().GetByIdAsync(id, ct);
        if (coupon is null)
            return null;

        return new CouponInputDto
        {
            Id = coupon.Id,
            Code = coupon.Code,
            Type = coupon.Type,
            Value = coupon.Value,
            MinAmount = coupon.MinAmount,
            MaxUses = coupon.MaxUses,
            StartsAt = coupon.StartsAt,
            ExpiresAt = coupon.ExpiresAt,
            IsActive = coupon.IsActive,
            Description = coupon.Description
        };
    }

    public async Task<int> SaveAsync(CouponInputDto dto, CancellationToken ct = default)
    {
        InputValidation.Check(new CouponInputValidator(), dto);
        var repo = _unitOfWork.Repository<Coupon>();
        Coupon coupon;
        var isNew = dto.Id == 0;
        if (await repo.AnyAsync(c => c.Id != dto.Id && c.Code == dto.Code.Trim().ToUpper(), ct))
            throw new InvalidOperationException("Ya existe un cupón con ese código.");

        if (isNew)
        {
            var codeExists = await repo.AnyAsync(c => c.Code.ToUpper() == dto.Code.Trim().ToUpper(), ct);
            if (codeExists)
                throw new InvalidOperationException("Ya existe un cupón con ese código.");

            coupon = new Coupon();
            await repo.AddAsync(coupon, ct);
        }
        else
        {
            coupon = (await repo.GetByIdAsync(dto.Id, ct))!;
            if (coupon is null)
                throw new InvalidOperationException("El cupón no existe.");
        }

        coupon.Code = dto.Code.Trim().ToUpperInvariant();
        coupon.Type = dto.Type;
        coupon.Value = dto.Value;
        coupon.MinAmount = dto.MinAmount;
        coupon.MaxUses = dto.MaxUses;
        coupon.StartsAt = dto.StartsAt;
        coupon.ExpiresAt = dto.ExpiresAt;
        coupon.IsActive = dto.IsActive;
        coupon.Description = dto.Description;

        if (!isNew)
            repo.Update(coupon);

        await _unitOfWork.SaveChangesAsync(ct);
        return coupon.Id;
    }

    public async Task<bool> ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Coupon>();
        var coupon = await repo.GetByIdAsync(id, ct);
        if (coupon is null)
            return false;

        coupon.IsActive = !coupon.IsActive;
        repo.Update(coupon);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Coupon>();
        var coupon = await repo.GetByIdAsync(id, ct);
        if (coupon is null)
            return false;

        repo.Remove(coupon);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

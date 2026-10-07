using Bisuteria.Application.DTOs.Admin;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de administración de cupones de descuento.
/// </summary>
public interface ICouponAdminService
{
    Task<List<CouponDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<CouponInputDto?> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    Task<int> SaveAsync(CouponInputDto dto, CancellationToken cancellationToken = default);

    Task<bool> ToggleActiveAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

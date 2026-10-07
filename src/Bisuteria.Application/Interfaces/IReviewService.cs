using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Catalog;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de reseñas y opiniones de clientes.
/// </summary>
public interface IReviewService
{
    Task<List<ReviewDto>> GetApprovedAsync(int? productId = null, CancellationToken cancellationToken = default);

    Task<bool> CreateAsync(CreateReviewDto dto, string? userId, string? customerName, CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(int reviewId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int reviewId, CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    Task<List<ReviewAdminDto>> GetAdminListAsync(bool pendingOnly = false, CancellationToken cancellationToken = default);
}

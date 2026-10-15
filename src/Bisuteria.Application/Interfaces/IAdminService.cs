using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de administración: dashboard, inventario, usuarios y reportes.
/// </summary>
public interface IAdminService
{
    Task<IReadOnlyList<AdminSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<DashboardStats> GetDashboardAsync(CancellationToken cancellationToken = default, DateTime? from = null, DateTime? to = null, string interval = "day", OrderStatus? status = null);

    Task<int> AddStockAsync(int productId, int quantity, string reason, string? createdBy, CancellationToken cancellationToken = default);

    Task<int> RemoveStockAsync(int productId, int quantity, string reason, string? createdBy, CancellationToken cancellationToken = default);

    Task<List<(int ProductId, string Name, int Stock, int MinStock)>> GetLowStockAsync(CancellationToken cancellationToken = default);

    /// <summary>Historial de movimientos de inventario (más recientes primero).</summary>
    Task<List<StockMovementRow>> GetStockMovementsAsync(int? productId = null, int limit = 100, CancellationToken cancellationToken = default);

    Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default, int page = 1, int pageSize = 25, string? userId = null);

    Task<bool> SetUserActiveAsync(string userId, bool isActive, CancellationToken cancellationToken = default);

    Task<List<SalesReportRow>> GetSalesReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default, PaymentStatus? payment = null);

    Task<List<ProductReportRow>> GetProductReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    Task<List<SalesReportRow>> GetReportByPeriodAsync(OrderStatus? status = null, CancellationToken cancellationToken = default);
}

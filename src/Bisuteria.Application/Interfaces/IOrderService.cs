using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de pedidos: creación, consulta y gestión de estados.
/// </summary>
public interface IOrderService
{
    Task<string?> GetAdminNotesAsync(int orderId, CancellationToken ct = default);
    /// <summary>Crea un pedido a partir del checkout y del carrito.</summary>
    Task<OrderDto> CreateAsync(CheckoutDto checkout, CartDto cart, string? userId, CancellationToken cancellationToken = default);

    Task<OrderDto?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task<List<OrderDto>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<PaginatedOrdersResult> GetPagedAsync(int page, int pageSize, OrderStatus? status = null, string? search = null, CancellationToken cancellationToken = default, string? attention = null);

    Task<bool> UpdateStatusAsync(int orderId, OrderStatus status, string? adminNotes = null, CancellationToken cancellationToken = default);

    Task<bool> UpdatePaymentStatusAsync(int orderId, PaymentStatus status, CancellationToken cancellationToken = default);

    Task<bool> ConfirmShippingAsync(int orderId, decimal shippingCost, string actorId, string actorRole, CancellationToken cancellationToken = default);

    Task<bool> CanViewGuestOrderAsync(string orderNumber, string accessToken, CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);
}

public record PaginatedOrdersResult(
    IReadOnlyList<OrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

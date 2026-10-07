using Bisuteria.Application.Validators;
using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de pedidos.
/// </summary>
public class OrderService : IOrderService
{
    public Task<string?> GetAdminNotesAsync(int orderId, CancellationToken ct = default)
        => _unitOfWork.Repository<Order>().Query().Where(o => o.Id == orderId).Select(o => o.AdminNotes).FirstOrDefaultAsync(ct);

    private const string OrderPrefix = "BISU-";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICouponService _couponService;
    private readonly ISettingService _settings;

    public OrderService(IUnitOfWork unitOfWork, ICouponService couponService, ISettingService settings)
    {
        _unitOfWork = unitOfWork;
        _couponService = couponService;
        _settings = settings;
    }

    public Task<OrderDto> CreateAsync(CheckoutDto checkout, CartDto cart, string? userId, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => CreateCoreAsync(checkout, cart, userId, ct), ct);

    private async Task<OrderDto> CreateCoreAsync(CheckoutDto checkout, CartDto cart, string? userId, CancellationToken ct = default)
    {
        InputValidation.Check(new CheckoutValidator(), checkout);
        if (cart.Items.Any(i => i.Quantity < 1 || i.Quantity > 50 || i.UnitPrice < 0))
            throw new InvalidOperationException("El carrito contiene cantidades o precios inválidos.");
        if (cart.IsEmpty)
            throw new InvalidOperationException("El carrito está vacío.");

        var orderRepo = _unitOfWork.Repository<Order>();
        var productRepo = _unitOfWork.Repository<Product>();
        var customizationRepo = _unitOfWork.Repository<Customization>();
        var stockRepo = _unitOfWork.Repository<StockMovement>();

        // Leer y recalcular dentro de la misma transacción que reserva el inventario.
        var ids = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await productRepo.Query().Include(p => p.Category).Include(p => p.Images)
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var group in cart.Items.GroupBy(i => i.ProductId))
        {
            if (!products.TryGetValue(group.Key, out var product) || !product.IsActive || product.Category?.IsActive != true)
                throw new InvalidOperationException("Un producto del carrito ya no está a la venta.");
            if (product.Stock < group.Sum(i => i.Quantity))
                throw new InvalidOperationException($"Stock insuficiente para '{product.Name}'. Disponibles: {product.Stock}.");
        }

        var lines = new List<OrderItem>();
        foreach (var item in cart.Items)
        {
            var product = products[item.ProductId];
            Customization? customization = null;
            if (item.CustomizationId.HasValue)
            {
                customization = await customizationRepo.GetByIdAsync(item.CustomizationId.Value, ct);
                if (customization is null || customization.ProductId != product.Id || !product.IsCustomizable)
                    throw new InvalidOperationException("La personalización no corresponde a un producto disponible.");
            }
            lines.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.MainImageUrl,
                UnitPrice = customization is null ? product.EffectivePrice
                    : Pricing.ComputeCustomizationPrice(EntityMappings.ToInput(customization), product.EffectivePrice),
                Quantity = item.Quantity,
                CustomizationId = customization?.Id,
                CustomizationSummary = customization?.BuildSummary()
            });
        }
        var subtotal = lines.Sum(i => i.LineTotal);
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var coupon = await _couponService.ValidateAsync(cart.CouponCode, subtotal, ct);
            if (!coupon.IsValid) throw new InvalidOperationException(coupon.Message);
            discount = coupon.Discount;
        }
        var threshold = await _settings.GetFreeShippingThresholdAsync(ct);
        var shippingCost = checkout.DeliveryMethod != DeliveryMethod.Shipping ||
            (threshold.HasValue && subtotal >= threshold.Value) ? 0m : await _settings.GetShippingCostAsync(ct);

        var order = new Order
        {
            OrderNumber = await GenerateOrderNumberAsync(ct),
            UserId = userId,
            CustomerName = checkout.CustomerName,
            CustomerEmail = checkout.CustomerEmail,
            CustomerPhone = checkout.CustomerPhone,
            AddressLine1 = checkout.AddressLine1,
            AddressLine2 = checkout.AddressLine2,
            City = checkout.City,
            Province = checkout.Province,
            PostalCode = checkout.PostalCode,
            Observations = checkout.Observations,
            Status = OrderStatus.Pending,
            PaymentMethod = checkout.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            Subtotal = subtotal,
            Discount = discount,
            ShippingCost = shippingCost,
            Total = Math.Max(0, subtotal - discount) + shippingCost,
            DeliveryMethod = checkout.DeliveryMethod,
            IsGift = checkout.IsGift,
            GiftRecipientName = checkout.IsGift ? checkout.GiftRecipientName : null,
            GiftMessage = checkout.IsGift ? checkout.GiftMessage : null
        };

        foreach (var item in lines)
        {
            var product = products[item.ProductId!.Value];
            order.Items.Add(item);

            // Descontar inventario y registrar movimiento.
            if (product is not null)
            {
                product.Stock -= item.Quantity;
                product.SoldCount += item.Quantity;
                productRepo.Update(product);

                await stockRepo.AddAsync(new StockMovement
                {
                    ProductId = product.Id,
                    Type = StockMovementType.Out,
                    Quantity = item.Quantity,
                    StockAfter = product.Stock,
                    Reason = $"Venta - Pedido {order.OrderNumber}"
                }, ct);
            }
        }

        order.Payments.Add(new Payment
        {
            Method = checkout.PaymentMethod,
            Status = PaymentStatus.Pending,
            Amount = order.Total
        });

        await orderRepo.AddAsync(order, ct);

        // Registrar uso del cupón.
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
            await _couponService.RegisterUseAsync(cart.CouponCode, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return EntityMappings.ToOrder(order);
    }

    public async Task<OrderDto?> GetByNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        var order = await LoadOrderQuery()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        return order is null ? null : EntityMappings.ToOrder(order);
    }

    public async Task<List<OrderDto>> GetByUserAsync(string userId, CancellationToken ct = default)
    {
        var orders = await LoadOrderQuery()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return orders.Select(EntityMappings.ToOrder).ToList();
    }

    public async Task<PaginatedOrdersResult> GetPagedAsync(int page, int pageSize, OrderStatus? status = null, string? search = null, CancellationToken ct = default)
    {
        var q = LoadOrderQuery();

        if (status.HasValue)
            q = q.Where(o => o.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(term) ||
                             o.CustomerName.Contains(term) ||
                             o.CustomerEmail.Contains(term));
        }

        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var orders = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new PaginatedOrdersResult(
            orders.Select(EntityMappings.ToOrder).ToList(),
            total,
            page,
            pageSize,
            totalPages);
    }

    public Task<bool> UpdateStatusAsync(int orderId, OrderStatus status, string? adminNotes = null, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => UpdateStatusCoreAsync(orderId, status, adminNotes, ct), ct);

    private async Task<bool> UpdateStatusCoreAsync(int orderId, OrderStatus status, string? adminNotes = null, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Order>();
        var order = await repo.Query().Include(o => o.Items).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return false;

        if (!Enum.IsDefined(status)) return false;
        if (adminNotes?.Length > 2000) return false;
        if (order.Status == OrderStatus.Cancelled && status != OrderStatus.Cancelled) return false;
        var statusChanged = order.Status != status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        if (adminNotes is not null)
            order.AdminNotes = adminNotes;

        // Si el pedido se cancela, reponer el inventario.
        if (statusChanged && status == OrderStatus.Cancelled)
            await RestoreStockAsync(order, ct);

        repo.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> UpdatePaymentStatusAsync(int orderId, PaymentStatus status, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => UpdatePaymentStatusCoreAsync(orderId, status, ct), ct);

    private async Task<bool> UpdatePaymentStatusCoreAsync(int orderId, PaymentStatus status, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Order>();
        var order = await repo.Query().Include(o => o.Items).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return false;

        if (!Enum.IsDefined(status)) return false;
        order.PaymentStatus = status;
        foreach (var payment in order.Payments) payment.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        repo.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CountByStatusAsync(OrderStatus status, CancellationToken ct = default)
        => await _unitOfWork.Repository<Order>().CountAsync(o => o.Status == status, ct);

    private IQueryable<Order> LoadOrderQuery()
        => _unitOfWork.Repository<Order>().Query()
            .Include(o => o.Items)
            .ThenInclude(i => i.Customization)
            .Include(o => o.Payments);

    private async Task RestoreStockAsync(Order order, CancellationToken ct)
    {
        var productRepo = _unitOfWork.Repository<Product>();
        var stockRepo = _unitOfWork.Repository<StockMovement>();

        foreach (var item in order.Items)
        {
            if (!item.ProductId.HasValue)
                continue;

            var product = await productRepo.GetByIdAsync(item.ProductId.Value, ct);
            if (product is null)
                continue;

            product.Stock += item.Quantity;
            product.SoldCount = Math.Max(0, product.SoldCount - item.Quantity);
            productRepo.Update(product);

            await stockRepo.AddAsync(new StockMovement
            {
                ProductId = product.Id,
                Type = StockMovementType.In,
                Quantity = item.Quantity,
                StockAfter = product.Stock,
                Reason = $"Cancelación - Pedido {order.OrderNumber}"
            }, ct);
        }
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        var last = await _unitOfWork.Repository<Order>().Query()
            .OrderByDescending(o => o.Id)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);

        int seq = 1;
        if (!string.IsNullOrEmpty(last) && last.StartsWith(OrderPrefix) &&
            int.TryParse(last[OrderPrefix.Length..], out var lastNumber))
            seq = lastNumber + 1;

        return $"{OrderPrefix}{seq:D6}";
    }
}

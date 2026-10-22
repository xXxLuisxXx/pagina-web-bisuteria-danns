using Bisuteria.Application.Validators;
using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

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
            throw new Bisuteria.Application.Services.BusinessRuleException("El carrito contiene cantidades o precios inválidos.");
        if (cart.IsEmpty)
            throw new Bisuteria.Application.Services.BusinessRuleException("El carrito está vacío.");

        var orderRepo = _unitOfWork.Repository<Order>();
        if (checkout.CheckoutToken.HasValue)
        {
            var existing = await LoadOrderQuery().SingleOrDefaultAsync(o => o.CheckoutToken == checkout.CheckoutToken, ct);
            if (existing is not null)
            {
                var existingDto = EntityMappings.ToOrder(existing);
                if (IsGuestTokenMatch(existing.GuestAccessTokenHash, checkout.GuestAccessToken))
                    existingDto.GuestAccessToken = checkout.GuestAccessToken;
                return existingDto;
            }
        }
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
                throw new Bisuteria.Application.Services.BusinessRuleException("Un producto del carrito ya no está a la venta.");
            if (product.Stock < group.Sum(i => i.Quantity))
                throw new Bisuteria.Application.Services.BusinessRuleException($"Stock insuficiente para '{product.Name}'. Disponibles: {product.Stock}.");
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
                    throw new Bisuteria.Application.Services.BusinessRuleException("La personalización no corresponde a un producto disponible.");
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
            if (bool.TryParse(await _settings.GetAsync("CouponSingleUsePerCustomer", "false", ct), out var singleUse) && singleUse)
            {
                if (string.IsNullOrWhiteSpace(userId)) throw new Bisuteria.Application.Services.BusinessRuleException("Inicia sesión para usar un cupón.");
                var normalized = cart.CouponCode.Trim().ToUpperInvariant();
                if (await orderRepo.AnyAsync(o => o.UserId == userId && o.CouponCode == normalized && o.Status != OrderStatus.Cancelled, ct))
                    throw new Bisuteria.Application.Services.BusinessRuleException("Ya utilizaste este cupón en otro pedido vigente.");
            }
            var coupon = await _couponService.ValidateAsync(cart.CouponCode, subtotal, ct);
            if (!coupon.IsValid) throw new Bisuteria.Application.Services.BusinessRuleException(coupon.Message);
            discount = coupon.Discount;
        }
        var shippingToConfirm = false;
        var shippingCost = 0m;
        DeliveryZone? deliveryZone = null;
        if (checkout.DeliveryMethod != DeliveryMethod.Pickup)
        {
            if (!checkout.DeliveryZoneId.HasValue)
                throw new BusinessRuleException("Selecciona un sector de entrega.");
            deliveryZone = await _unitOfWork.Repository<DeliveryZone>().FirstOrDefaultAsync(zone =>
                zone.Id == checkout.DeliveryZoneId.Value && zone.IsActive && zone.Province == "Manabí" &&
                (zone.City == "Manta" || zone.City == "Jaramijó"), ct);
            if (deliveryZone is null)
                throw new BusinessRuleException("El sector seleccionado ya no está disponible. Actualiza la página y elige otro.");
            if (deliveryZone.RequiresConfirmation)
            {
                if (deliveryZone.IsFree || deliveryZone.Cost != 0m)
                    throw new BusinessRuleException("La tarifa de este sector requiere revisión de la tienda.");
                shippingToConfirm = true;
            }
            else if (deliveryZone.IsFree)
            {
                if (deliveryZone.Cost != 0m)
                    throw new BusinessRuleException("La zona gratuita tiene una tarifa inválida.");
            }
            else if (deliveryZone.Cost <= 0m || deliveryZone.Cost > 10000m || decimal.Round(deliveryZone.Cost, 2) != deliveryZone.Cost)
            {
                throw new BusinessRuleException("La tarifa del sector no está configurada correctamente.");
            }
            else shippingCost = deliveryZone.Cost;
        }

        var guestTokenHash = string.IsNullOrWhiteSpace(userId) ? HashGuestToken(checkout.GuestAccessToken) : null;

        var order = new Order
        {
            CheckoutToken = checkout.CheckoutToken,
            CouponCode = string.IsNullOrWhiteSpace(cart.CouponCode) ? null : cart.CouponCode.Trim().ToUpperInvariant(),
            OrderNumber = $"{OrderPrefix}{Guid.NewGuid():N}"[..29],
            UserId = userId,
            CustomerName = checkout.CustomerName,
            CustomerEmail = checkout.CustomerEmail,
            CustomerPhone = checkout.CustomerPhone,
            AddressLine1 = checkout.AddressLine1,
            AddressLine2 = checkout.AddressLine2,
            City = deliveryZone?.City ?? string.Empty,
            Province = deliveryZone?.Province ?? string.Empty,
            DeliverySector = deliveryZone?.Sector,
            DeliveryZoneId = deliveryZone?.Id,
            DeliveryZoneName = deliveryZone?.Sector,
            Observations = checkout.Observations,
            Status = OrderStatus.Pending,
            PaymentMethod = checkout.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            Subtotal = subtotal,
            Discount = discount,
            ShippingCost = shippingCost,
            Total = Math.Max(0, subtotal - discount) + shippingCost,
            ShippingToConfirm = shippingToConfirm,
            GuestAccessTokenHash = guestTokenHash,
            GuestAccessToken = guestTokenHash is null ? null : checkout.GuestAccessToken,
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

    public async Task<PaginatedOrdersResult> GetPagedAsync(int page, int pageSize, OrderStatus? status = null, string? search = null, CancellationToken ct = default, string? attention = null)
    {
        var q = LoadOrderQuery();

        if (attention is not (null or "" or "proofs" or "shipping" or "unpaid" or "old"))
            throw new BusinessRuleException("Filtro de atención no válido.");
        if (attention == "proofs")
            q = q.Where(o => o.Status != OrderStatus.Cancelled && o.PaymentProofs.Any(proof => proof.Status == PaymentProofStatus.Pending));
        else if (attention == "shipping")
            q = q.Where(o => o.ShippingToConfirm && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered);
        else if (attention == "unpaid")
            q = q.Where(o => o.Status != OrderStatus.Cancelled && o.PaymentStatus == PaymentStatus.Pending);
        else if (attention == "old")
        {
            var oldLimit = DateTime.UtcNow.AddDays(-2);
            q = q.Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < oldLimit);
        }

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
        if (!OrderTransitions.Allows(order.Status, status, order.PaymentStatus)) return false;
        var statusChanged = order.Status != status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        if (adminNotes is not null)
            order.AdminNotes = adminNotes;

        // Si el pedido se cancela, reponer el inventario.
        if (statusChanged && status == OrderStatus.Cancelled)
        {
            await RestoreStockAsync(order, ct);
            if (order.CouponCode is not null)
            {
                var coupon = await _unitOfWork.Repository<Coupon>().FirstOrDefaultAsync(c => c.Code == order.CouponCode, ct);
                if (coupon is not null)
                {
                    coupon.UsedCount = Math.Max(0, coupon.UsedCount - 1);
                    _unitOfWork.Repository<Coupon>().Update(coupon);
                }
            }
        }

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

        if (!OrderTransitions.AllowsPayment(order.Status, order.PaymentStatus, status)) return false;
        if (order.ShippingToConfirm && status == PaymentStatus.Paid) return false;
        order.PaymentStatus = status;
        foreach (var payment in order.Payments)
        {
            payment.Status = status;
            if (status == PaymentStatus.Paid && payment.PaidAt is null)
                payment.PaidAt = DateTime.UtcNow;
        }
        order.UpdatedAt = DateTime.UtcNow;
        repo.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> ConfirmShippingAsync(int orderId, decimal shippingCost, string actorId, string actorRole, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(async () =>
        {
            if (shippingCost is < 0 or > 10000 || decimal.Round(shippingCost, 2) != shippingCost ||
                string.IsNullOrWhiteSpace(actorId) || actorRole is not (StoreRoles.Owner or StoreRoles.Developer or StoreRoles.LegacyAdmin)) return false;
            var order = await _unitOfWork.Repository<Order>().Query().Include(o => o.Payments).Include(o => o.PaymentProofs)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null || order.DeliveryMethod == DeliveryMethod.Pickup || order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded ||
                order.Status is OrderStatus.Cancelled or OrderStatus.Delivered ||
                order.PaymentProofs.Any(proof => proof.Status is PaymentProofStatus.Pending or PaymentProofStatus.Approved))
                return false;

            var oldShipping = order.ShippingCost;
            var oldTotal = order.Total;
            order.ShippingCost = decimal.Round(shippingCost, 2, MidpointRounding.AwayFromZero);
            order.Total = Math.Max(0m, order.Subtotal - order.Discount) + order.ShippingCost;
            order.ShippingToConfirm = false;
            order.UpdatedAt = DateTime.UtcNow;
            foreach (var payment in order.Payments) payment.Amount = order.Total;
            _unitOfWork.Repository<Order>().Update(order);
            await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
            {
                ActorId = actorId,
                ActorRole = actorRole,
                Action = "Order.ShippingConfirmed",
                EntityType = "Order",
                EntityId = order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Outcome = "Success",
                BeforeJson = System.Text.Json.JsonSerializer.Serialize(new { ShippingCost = oldShipping, Total = oldTotal }),
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { ShippingCost = order.ShippingCost, order.Total })
            });
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);

    public async Task<bool> CanViewGuestOrderAsync(string orderNumber, string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 128) return false;
        var savedHash = await _unitOfWork.Repository<Order>().Query().Where(o => o.OrderNumber == orderNumber)
            .Select(o => o.GuestAccessTokenHash).FirstOrDefaultAsync(ct);
        return IsGuestTokenMatch(savedHash, accessToken);
    }

    private static string? HashGuestToken(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool IsGuestTokenMatch(string? expectedHash, string? token)
    {
        if (string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(token)) return false;
        var actualHash = HashGuestToken(token)!;
        return expectedHash.Length == actualHash.Length && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash), Encoding.ASCII.GetBytes(actualHash));
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

}

using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Infrastructure.Services;

/// <summary>
/// Implementación del servicio administrativo (dashboard, inventario, usuarios y reportes).
/// Requiere acceso a Identity, por lo que reside en la capa de Infraestructura.
/// </summary>
public class AdminService : IAdminService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderService _orderService;

    public AdminService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IOrderService orderService)
    {
        _context = context;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _orderService = orderService;
    }

    public async Task<DashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var twelveMonthsAgo = monthStart.AddMonths(-11);

        var orders = _context.Orders;
        var items = _context.OrderItems;

        var salesToday = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= todayStart)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        var ordersToday = await orders
            .CountAsync(o => o.CreatedAt >= todayStart, ct);

        var salesMonth = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= monthStart)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        var ordersMonth = await orders.CountAsync(o => o.CreatedAt >= monthStart, ct);

        var pendingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Pending, ct);

        var productsSold = await items.Where(i => i.Order != null && i.Order.Status != OrderStatus.Cancelled).SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        var registeredClients = await _userManager.Users.CountAsync(u => u.IsActive, ct);

        var totalProducts = await _context.Products.CountAsync(p => p.IsActive, ct);

        var availableProducts = await _context.Products.CountAsync(p => p.IsActive && p.Stock > 0, ct);

        var soldOutProducts = await _context.Products.CountAsync(p => p.IsActive && p.Stock == 0, ct);

        var categoriesCount = await _context.Categories.CountAsync(c => c.IsActive, ct);

        var totalOrders = await orders.CountAsync(ct);

        var totalSales = await orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        var lowStock = await _context.Products.CountAsync(p => p.IsActive && p.Stock <= p.MinStock, ct);

        var pendingReviews = await _context.Reviews.CountAsync(r => !r.IsApproved, ct);

        var unreadMessages = await _context.ContactMessages.CountAsync(m => !m.IsRead, ct);

        // Ventas de los últimos 12 meses.
        var salesByMonth = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= twelveMonthsAgo)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(o => o.Total) })
            .ToListAsync(ct);

        var monthLabels = Enumerable.Range(0, 12)
            .Select(i => twelveMonthsAgo.AddMonths(i))
            .ToList();

        var salesChart = monthLabels.Select(m => new ChartPoint
        {
            Label = m.ToString("MMM"),
            Value = salesByMonth
                .FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0
        }).ToList();

        // Pedidos por estado.
        var ordersByStatus = await orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusLabels = new Dictionary<OrderStatus, string>
        {
            [OrderStatus.Pending] = "Pendientes",
            [OrderStatus.Confirmed] = "Confirmados",
            [OrderStatus.Preparing] = "En preparación",
            [OrderStatus.Ready] = "Listos",
            [OrderStatus.Shipped] = "Enviados",
            [OrderStatus.Delivered] = "Entregados",
            [OrderStatus.Cancelled] = "Cancelados"
        };

        var statusChart = Enum.GetValues<OrderStatus>().Select(s => new ChartPoint
        {
            Label = statusLabels[s],
            Value = ordersByStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0
        }).ToList();

        // Top productos vendidos.
        var topProducts = await items
            .Where(i => i.ProductId != null && i.Order != null && i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductId!)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.UnitPrice * i.Quantity)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(5)
            .ToListAsync(ct);

        var topProductDtos = new List<TopProductDto>();
        foreach (var tp in topProducts)
        {
            var product = await _context.Products.AsNoTracking()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == tp.ProductId, ct);
            if (product is null)
                continue;
            topProductDtos.Add(new TopProductDto
            {
                ProductId = product.Id,
                Name = product.Name,
                ImageUrl = product.Images.OrderBy(i => i.IsPrimary ? 0 : 1).FirstOrDefault()?.ImageUrl,
                QuantitySold = tp.Quantity,
                Revenue = tp.Revenue
            });
        }

        // Pedidos recientes.
        var recentOrders = await _orderService.GetPagedAsync(1, 5, null, null, ct);

        return new DashboardStats
        {
            SalesToday = salesToday,
            OrdersToday = ordersToday,
            SalesMonth = salesMonth,
            OrdersMonth = ordersMonth,
            PendingOrders = pendingOrders,
            ProductsSold = productsSold,
            RegisteredClients = registeredClients,
            TotalProducts = totalProducts,
            AvailableProducts = availableProducts,
            SoldOutProducts = soldOutProducts,
            CategoriesCount = categoriesCount,
            TotalOrders = totalOrders,
            TotalSales = totalSales,
            LowStockCount = lowStock,
            PendingReviews = pendingReviews,
            UnreadMessages = unreadMessages,
            SalesByMonth = salesChart,
            OrdersByStatus = statusChart,
            TopProducts = topProductDtos,
            RecentOrders = recentOrders.Items
        };
    }

    public Task<int> AddStockAsync(int productId, int quantity, string reason, string? createdBy, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => AddStockCoreAsync(productId, quantity, reason, createdBy, ct), ct);

    private async Task<int> AddStockCoreAsync(int productId, int quantity, string reason, string? createdBy, CancellationToken ct)
    {
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId, ct);
        if (product is null || quantity <= 0 || quantity > 1000000 || string.IsNullOrWhiteSpace(reason) || reason.Length > 300)
            return -1;

        if (product.Stock > int.MaxValue - quantity) return -1;
        product.Stock += quantity;
        product.UpdatedAt = DateTime.UtcNow;

        var movementRepo = _unitOfWork.Repository<StockMovement>();
        await movementRepo.AddAsync(new StockMovement
        {
            ProductId = product.Id,
            Type = StockMovementType.In,
            Quantity = quantity,
            StockAfter = product.Stock,
            Reason = reason,
            CreatedBy = createdBy
        }, ct);

        _unitOfWork.Repository<Product>().Update(product);
        await _unitOfWork.SaveChangesAsync(ct);
        return product.Stock;
    }

    public Task<int> RemoveStockAsync(int productId, int quantity, string reason, string? createdBy, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => RemoveStockCoreAsync(productId, quantity, reason, createdBy, ct), ct);

    private async Task<int> RemoveStockCoreAsync(int productId, int quantity, string reason, string? createdBy, CancellationToken ct)
    {
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId, ct);
        if (product is null || quantity <= 0 || quantity > 1000000 || string.IsNullOrWhiteSpace(reason) || reason.Length > 300)
            return -1;

        if (quantity > product.Stock) return -1;
        product.Stock -= quantity;
        product.UpdatedAt = DateTime.UtcNow;

        var movementRepo = _unitOfWork.Repository<StockMovement>();
        await movementRepo.AddAsync(new StockMovement
        {
            ProductId = product.Id,
            Type = StockMovementType.Out,
            Quantity = quantity,
            StockAfter = product.Stock,
            Reason = reason,
            CreatedBy = createdBy
        }, ct);

        _unitOfWork.Repository<Product>().Update(product);
        await _unitOfWork.SaveChangesAsync(ct);
        return product.Stock;
    }

    public async Task<List<(int ProductId, string Name, int Stock, int MinStock)>> GetLowStockAsync(CancellationToken ct = default)
    {
        var products = await _context.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock)
            .Select(p => new { p.Id, p.Name, p.Stock, p.MinStock })
            .ToListAsync(ct);

        return products.Select(p => (p.Id, p.Name, p.Stock, p.MinStock)).ToList();
    }

    public async Task<List<StockMovementRow>> GetStockMovementsAsync(int? productId = null, int limit = 100, CancellationToken ct = default)
    {
        var query = _context.StockMovements.AsNoTracking()
            .Include(m => m.Product)
            .AsQueryable();

        if (productId.HasValue)
            query = query.Where(m => m.ProductId == productId.Value);

        var movements = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new StockMovementRow
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductName = m.Product != null ? m.Product.Name : $"#{m.ProductId}",
                Type = m.Type == StockMovementType.In ? "Entrada" : "Salida",
                Quantity = m.Quantity,
                StockAfter = m.StockAfter,
                Reason = m.Reason,
                CreatedBy = m.CreatedBy,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        return movements;
    }

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        var orderCounts = await _context.Orders
            .Where(o => o.UserId != null)
            .GroupBy(o => o.UserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var totals = await _context.Orders
            .Where(o => o.UserId != null && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.UserId!)
            .Select(g => new { UserId = g.Key, Total = g.Sum(o => o.Total) })
            .ToDictionaryAsync(x => x.UserId, x => x.Total, ct);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
                City = user.City,
                Province = user.Province,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                Roles = roles.ToArray(),
                OrderCount = orderCounts.GetValueOrDefault(user.Id),
                TotalSpent = totals.GetValueOrDefault(user.Id)
            });
        }

        return result;
    }

    public async Task<bool> SetUserActiveAsync(string userId, bool isActive, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return false;

        if (!isActive && await _userManager.IsInRoleAsync(user, "Admin")) return false;
        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded && !isActive) await _userManager.UpdateSecurityStampAsync(user);
        return result.Succeeded;
    }

    public async Task<List<SalesReportRow>> GetSalesReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        IQueryable<Order> q = _context.Orders.AsNoTracking().Include(o => o.Items);
        if (from.HasValue)
            q = q.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(o => o.CreatedAt <= to.Value.AddDays(1));

        var orders = await q.OrderBy(o => o.CreatedAt).ToListAsync(ct);

        return orders.Select(o => new SalesReportRow
        {
            OrderNumber = o.OrderNumber,
            Date = o.CreatedAt,
            Customer = o.CustomerName,
            Province = o.Province,
            Status = o.Status.GetLabel(),
            PaymentMethod = o.PaymentMethod == PaymentMethod.BankTransfer ? "Transferencia bancaria" : o.PaymentMethod == PaymentMethod.Card ? "Tarjeta" : "Contra entrega",
            Items = o.Items.Sum(i => i.Quantity),
            Subtotal = o.Subtotal,
            Discount = o.Discount,
            Shipping = o.ShippingCost,
            Total = o.Total
        }).ToList();
    }

    public async Task<List<ProductReportRow>> GetProductReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var q = _context.OrderItems.AsNoTracking().Where(i => i.ProductId != null && i.Order != null && i.Order.Status != OrderStatus.Cancelled);
        if (from.HasValue)
            q = q.Where(i => i.Order != null && i.Order.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(i => i.Order != null && i.Order.CreatedAt <= to.Value.AddDays(1));

        var rows = await q
            .GroupBy(i => i.ProductId!)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.UnitPrice * i.Quantity)
            })
            .ToListAsync(ct);

        var productIds = rows.Select(r => r.ProductId).ToList();
        var products = await _context.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var result = new List<ProductReportRow>();
        foreach (var row in rows)
        {
            if (!products.TryGetValue(row.ProductId!.Value, out var product)) continue;

            result.Add(new ProductReportRow
            {
                Name = product.Name,
                Category = product.Category?.Name ?? string.Empty,
                Sku = product.Sku ?? string.Empty,
                Price = product.Price,
                Stock = product.Stock,
                Sold = row.Quantity,
                Revenue = row.Revenue
            });
        }

        return result.OrderByDescending(r => r.Sold).ToList();
    }

    public async Task<List<SalesReportRow>> GetReportByPeriodAsync(OrderStatus? status = null, CancellationToken ct = default)
    {
        IQueryable<Order> q = _context.Orders.AsNoTracking().Include(o => o.Items);
        if (status.HasValue)
            q = q.Where(o => o.Status == status.Value);

        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

        return orders.Select(o => new SalesReportRow
        {
            OrderNumber = o.OrderNumber,
            Date = o.CreatedAt,
            Customer = o.CustomerName,
            Province = o.Province,
            Status = o.Status.GetLabel(),
            PaymentMethod = o.PaymentMethod == PaymentMethod.BankTransfer ? "Transferencia bancaria" : o.PaymentMethod == PaymentMethod.Card ? "Tarjeta" : "Contra entrega",
            Items = o.Items.Sum(i => i.Quantity),
            Subtotal = o.Subtotal,
            Discount = o.Discount,
            Shipping = o.ShippingCost,
            Total = o.Total
        }).ToList();
    }
}

using Bisuteria.Application.Services;
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
public partial class AdminService : IAdminService
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

    public Task<DashboardStats> GetDashboardAsync(CancellationToken ct = default, DateTime? from = null, DateTime? to = null, string interval = "day", OrderStatus? status = null)
        => BuildDashboardAsync(from, to, interval, status, ct);

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

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default, int page = 1, int pageSize = 25, string? userId = null)
    {
        var users = await _userManager.Users.AsNoTracking()
            .Where(u => userId == null || u.Id == userId)
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((Math.Clamp(page, 1, 10000) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100)).ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();
        var roleRows = await (from link in _context.UserRoles
                              join role in _context.Roles on link.RoleId equals role.Id
                              where ids.Contains(link.UserId)
                              select new { link.UserId, role.Name }).ToListAsync(ct);
        var rolesByUser = roleRows.ToLookup(r => r.UserId, r => r.Name!);

        var orderCounts = await _context.Orders
            .Where(o => o.UserId != null && ids.Contains(o.UserId))
            .GroupBy(o => o.UserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var totals = await _context.Orders
            .Where(o => o.UserId != null && ids.Contains(o.UserId) && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.UserId!)
            .Select(g => new { UserId = g.Key, Total = g.Sum(o => o.Total) })
            .ToDictionaryAsync(x => x.UserId, x => x.Total, ct);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = rolesByUser[user.Id];
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

    public async Task<List<SalesReportRow>> GetSalesReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default, PaymentStatus? payment = null)
    {
        IQueryable<Order> q = SalesCriteria.Apply(_context.Orders.AsNoTracking(), payment: payment).Include(o => o.Items);
        if (from.HasValue)
            q = SalesCriteria.Apply(q, from: from);
        if (to.HasValue)
            q = SalesCriteria.Apply(q, to: to);

        var orders = await q.OrderBy(o => o.CreatedAt).Select(o => new {
            o.OrderNumber, o.CreatedAt, o.CustomerName, o.Province, o.Status, o.PaymentMethod,
            Items = o.Items.Sum(i => i.Quantity), o.Subtotal, o.Discount, o.ShippingCost, o.Total
        }).ToListAsync(ct);

        return orders.Select(o => new SalesReportRow
        {
            OrderNumber = o.OrderNumber,
            Date = SalesCriteria.Local(o.CreatedAt),
            Customer = o.CustomerName,
            Province = o.Province,
            Status = o.Status.GetLabel(),
            PaymentMethod = o.PaymentMethod == PaymentMethod.BankTransfer ? "Transferencia bancaria" : o.PaymentMethod == PaymentMethod.Card ? "Tarjeta" : "Contra entrega",
            Items = o.Items,
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
            { var start = SalesCriteria.Utc(from.Value.Date); q = q.Where(i => i.Order != null && i.Order.CreatedAt >= start); }
        if (to.HasValue)
            { var end = SalesCriteria.Utc(to.Value.Date.AddDays(1)); q = q.Where(i => i.Order != null && i.Order.CreatedAt < end); }

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
        IQueryable<Order> q = SalesCriteria.Apply(_context.Orders.AsNoTracking()).Include(o => o.Items);
        if (status.HasValue)
            q = q.Where(o => o.Status == status.Value);

        var orders = await q.OrderByDescending(o => o.CreatedAt).Select(o => new {
            o.OrderNumber, o.CreatedAt, o.CustomerName, o.Province, o.Status, o.PaymentMethod,
            Items = o.Items.Sum(i => i.Quantity), o.Subtotal, o.Discount, o.ShippingCost, o.Total
        }).ToListAsync(ct);

        return orders.Select(o => new SalesReportRow
        {
            OrderNumber = o.OrderNumber,
            Date = SalesCriteria.Local(o.CreatedAt),
            Customer = o.CustomerName,
            Province = o.Province,
            Status = o.Status.GetLabel(),
            PaymentMethod = o.PaymentMethod == PaymentMethod.BankTransfer ? "Transferencia bancaria" : o.PaymentMethod == PaymentMethod.Card ? "Tarjeta" : "Contra entrega",
            Items = o.Items,
            Subtotal = o.Subtotal,
            Discount = o.Discount,
            Shipping = o.ShippingCost,
            Total = o.Total
        }).ToList();
    }
}

using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Services;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Infrastructure.Services;

public partial class AdminService
{
    private async Task<DashboardStats> BuildDashboardAsync(DateTime? from, DateTime? to, string interval, OrderStatus? status, CancellationToken ct)
    {
        var now = SalesCriteria.Local(DateTime.UtcNow);
        var start = (from ?? new DateTime(now.Year, now.Month, 1)).Date;
        var end = (to ?? now).Date;
        if (start.Year < 2000 || end.Year > 2100 || end < start || (end - start).TotalDays > 366 || interval is not ("day" or "week" or "month"))
            throw new BusinessRuleException("Selecciona un rango de hasta 367 días y una agrupación válida.");
        var length = (end - start).Days + 1;
        var previousStart = start.AddDays(-length);
        var previousEnd = start.AddDays(-1);
        var beginUtc = SalesCriteria.Utc(start);
        var endUtc = SalesCriteria.Utc(end.AddDays(1));
        var previousUtc = SalesCriteria.Utc(previousStart);
        var today = SalesCriteria.Utc(now.Date);
        var tomorrow = SalesCriteria.Utc(now.Date.AddDays(1));
        var yesterday = today.AddDays(-1);
        var month = SalesCriteria.Utc(new DateTime(now.Year, now.Month, 1));
        var lastMonth = month.AddMonths(-1);
        var nextMonth = month.AddMonths(1);
        var sales = SalesCriteria.Apply(_context.Orders.AsNoTracking());
        var totals = await sales.GroupBy(o => 1).Select(g => new {
            Total = g.Sum(o => o.Total),
            Today = g.Sum(o => o.CreatedAt >= today && o.CreatedAt < tomorrow ? o.Total : 0),
            Yesterday = g.Sum(o => o.CreatedAt >= yesterday && o.CreatedAt < today ? o.Total : 0),
            Month = g.Sum(o => o.CreatedAt >= month && o.CreatedAt < nextMonth ? o.Total : 0),
            LastMonth = g.Sum(o => o.CreatedAt >= lastMonth && o.CreatedAt < month ? o.Total : 0),
            Period = g.Sum(o => o.CreatedAt >= beginUtc && o.CreatedAt < endUtc ? o.Total : 0),
            Previous = g.Sum(o => o.CreatedAt >= previousUtc && o.CreatedAt < beginUtc ? o.Total : 0),
            Orders = g.Count(o => o.CreatedAt >= beginUtc && o.CreatedAt < endUtc),
            PreviousOrders = g.Count(o => o.CreatedAt >= previousUtc && o.CreatedAt < beginUtc),
            TodayOrders = g.Count(o => o.CreatedAt >= today && o.CreatedAt < tomorrow),
            MonthOrders = g.Count(o => o.CreatedAt >= month && o.CreatedAt < nextMonth)
        }).SingleOrDefaultAsync(ct);
        var range = SalesCriteria.Apply(_context.Orders.AsNoTracking(), start, end);
        var daily = await range.GroupBy(o => o.CreatedAt.AddHours(-5).Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(o => o.Total) }).ToListAsync(ct);
        DateTime Bucket(DateTime date) => interval == "month" ? new DateTime(date.Year, date.Month, 1)
            : interval == "week" ? date.AddDays(-((int)date.DayOfWeek + 6) % 7) : date;
        var chart = Enumerable.Range(0, length).Select(i => start.AddDays(i)).GroupBy(Bucket)
            .Select(g => new ChartPoint { Label = g.Key.ToString("yyyy-MM-dd"), Value = g.Sum(d => daily.FirstOrDefault(x => x.Date == d)?.Total ?? 0) }).ToList();
        var states = await _context.Orders.AsNoTracking().Where(o => o.CreatedAt >= beginUtc && o.CreatedAt < endUtc)
            .GroupBy(o => o.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var payments = await range.GroupBy(o => o.PaymentMethod).Select(g => new { Method = g.Key, Total = g.Sum(o => o.Total) }).ToListAsync(ct);
        var soldItems = _context.OrderItems.AsNoTracking().Where(i => range.Select(o => o.Id).Contains(i.OrderId));
        var categories = await soldItems.GroupBy(i => _context.Products.Where(p => p.Id == i.ProductId).Select(p => p.Category!.Name).FirstOrDefault() ?? "Sin categoría")
            .Select(g => new ChartPoint { Label = g.Key, Value = g.Sum(i => i.UnitPrice * i.Quantity) }).ToListAsync(ct);
        var top = await soldItems.GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new TopProductDto { ProductId = g.Key.ProductId ?? 0, Name = g.Key.ProductName, QuantitySold = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.UnitPrice * i.Quantity) })
            .OrderByDescending(p => p.QuantitySold).ThenBy(p => p.ProductId).Take(5).ToListAsync(ct);
        var inventory = await _context.Products.Where(p => p.IsActive).GroupBy(p => 1)
            .Select(g => new { Total = g.Count(), Available = g.Count(p => p.Stock > 0), Empty = g.Count(p => p.Stock == 0), Low = g.Count(p => p.Stock > 0 && p.Stock <= p.MinStock) }).SingleOrDefaultAsync(ct);
        var customerIds = from link in _context.UserRoles join role in _context.Roles on link.RoleId equals role.Id where role.Name == "Customer" select link.UserId;
        var clients = await _context.Users.Where(u => customerIds.Contains(u.Id)).GroupBy(u => 1)
            .Select(g => new { Total = g.Count(), New = g.Count(u => u.CreatedAt >= beginUtc && u.CreatedAt < endUtc), Previous = g.Count(u => u.CreatedAt >= previousUtc && u.CreatedAt < beginUtc) }).SingleOrDefaultAsync(ct);
        var oldLimit = DateTime.UtcNow.AddDays(-2);
        var pending = await _context.Orders.GroupBy(o => 1).Select(g => new {
            Total = g.Count(), Pending = g.Count(o => o.Status == OrderStatus.Pending),
            Old = g.Count(o => o.Status == OrderStatus.Pending && o.CreatedAt < oldLimit),
            Unpaid = g.Count(o => o.Status != OrderStatus.Cancelled && o.PaymentStatus == PaymentStatus.Pending),
            Shipping = g.Count(o => o.ShippingToConfirm && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered)
        }).SingleOrDefaultAsync(ct);
        var pendingProofs = await _context.PaymentProofs.CountAsync(proof => proof.Status == PaymentProofStatus.Pending &&
            proof.Order!.Status != OrderStatus.Cancelled, ct);
        var reviews = await _context.Reviews.CountAsync(r => !r.IsApproved, ct);
        var messages = await _context.ContactMessages.CountAsync(m => !m.IsRead, ct);
        var expiry = DateTime.UtcNow.AddDays(7);
        var utcNow = DateTime.UtcNow;
        var coupons = await _context.Coupons.CountAsync(c => c.IsActive && c.ExpiresAt >= utcNow && c.ExpiresAt <= expiry, ct);
        var recent = await _context.Orders.AsNoTracking().Where(o => o.CreatedAt >= beginUtc && o.CreatedAt < endUtc && (!status.HasValue || o.Status == status))
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).Take(8)
            .Select(o => new OrderDto { Id = o.Id, OrderNumber = o.OrderNumber, CustomerName = o.CustomerName, MainProductName = o.Items.OrderBy(i => i.Id).Select(i => i.ProductName).FirstOrDefault() ?? "Sin detalle", CreatedAt = o.CreatedAt, Status = o.Status, PaymentStatus = o.PaymentStatus, Total = o.Total }).ToListAsync(ct);
        var calendarStart = new DateTime(end.Year, end.Month, 1);
        var calendarEnd = calendarStart.AddMonths(1).AddDays(-1);
        var calendar = await SalesCriteria.Apply(_context.Orders.AsNoTracking(), calendarStart, calendarEnd)
            .GroupBy(o => o.CreatedAt.AddHours(-5).Date).Select(g => new { Date = g.Key, Count = g.Count(), Sales = g.Sum(o => o.Total) }).ToListAsync(ct);
        var goalText = await _context.SiteSettings.Where(s => s.Key == "MonthlySalesGoal").Select(s => s.Value).FirstOrDefaultAsync(ct);
        var goal = decimal.TryParse(goalText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedGoal) ? Math.Max(0, parsedGoal) : 0;
        return new DashboardStats {
            MonthlyGoal = goal, CalendarSales = calendar.Sum(d => d.Sales), CalendarDays = calendar.Select(d => new ChartPoint { Label = d.Date.ToString("yyyy-MM-dd"), Value = d.Count }).ToList(),
            From = start, To = end, Interval = interval, RecentStatus = status, GeneratedAt = DateTime.UtcNow,
            PeriodSales = totals?.Period ?? 0, PreviousSales = totals?.Previous ?? 0, PeriodOrders = totals?.Orders ?? 0, PreviousOrders = totals?.PreviousOrders ?? 0,
            SalesToday = totals?.Today ?? 0, PreviousDaySales = totals?.Yesterday ?? 0, SalesMonth = totals?.Month ?? 0, PreviousMonthSales = totals?.LastMonth ?? 0,
            OrdersToday = totals?.TodayOrders ?? 0, OrdersMonth = totals?.MonthOrders ?? 0, TotalSales = totals?.Total ?? 0,
            NewClients = clients?.New ?? 0, PreviousClients = clients?.Previous ?? 0, RegisteredClients = clients?.Total ?? 0,
            TotalProducts = inventory?.Total ?? 0, AvailableProducts = inventory?.Available ?? 0, SoldOutProducts = inventory?.Empty ?? 0, LowStockCount = inventory?.Low ?? 0,
            TotalOrders = pending?.Total ?? 0, PendingOrders = pending?.Pending ?? 0, PendingReviews = reviews, UnreadMessages = messages,
            SalesByMonth = chart, SalesByCategory = categories, SalesByPayment = payments.Select(p => new ChartPoint { Label = p.Method == PaymentMethod.BankTransfer ? "Transferencia" : p.Method == PaymentMethod.Card ? "Tarjeta" : "Contra entrega", Value = p.Total }).ToList(),
            OrdersByStatus = states.Select(s => new ChartPoint { Label = s.Status.GetLabel(), Value = s.Count }).ToList(), TopProducts = top, RecentOrders = recent,
            Alerts = [
                new("Pedidos pendientes", pending?.Pending ?? 0, "/Admin/Orders?status=Pending", StorePermissions.OrdersView),
                new("Comprobantes por revisar", pendingProofs, "/Admin/Orders?attention=proofs", StorePermissions.PaymentsReviewProof),
                new("Envíos por confirmar", pending?.Shipping ?? 0, "/Admin/Orders?attention=shipping", StorePermissions.OrdersSetShipping),
                new("Stock bajo", inventory?.Low ?? 0, "/Admin/Inventory", StorePermissions.InventoryAdjust),
                new("Agotados", inventory?.Empty ?? 0, "/Admin/Inventory", StorePermissions.InventoryAdjust),
                new("Pendientes hace más de 48 h", pending?.Old ?? 0, "/Admin/Orders?attention=old", StorePermissions.OrdersView),
                new("Pagos pendientes", pending?.Unpaid ?? 0, "/Admin/Orders?attention=unpaid", StorePermissions.PaymentsUpdate),
                new("Reseñas por aprobar", reviews, "/Admin/Reviews?pendingOnly=true", StorePermissions.ReviewsModerate),
                new("Mensajes sin leer", messages, "/Admin/Messages?unreadOnly=true", StorePermissions.MessagesView),
                new("Cupones vencen en 7 días", coupons, "/Admin/Coupons", StorePermissions.CouponsManage)
            ]
        };
    }
}

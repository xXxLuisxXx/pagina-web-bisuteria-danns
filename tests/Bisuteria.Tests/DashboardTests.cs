using System.Net;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Services;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ImportedOrderNumberDoesNotRestartNumbering()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(new Order { OrderNumber = "IMPORT-" + Guid.NewGuid().ToString("N")[..20], CustomerName = "Importado", CustomerEmail = "import@example.test", CustomerPhone = "2025550100" });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var cart = new Bisuteria.Application.DTOs.Cart.CartDto { Items = [new() { ProductId = f.ProductId, Quantity = 1 }] };
        var first = await service.CreateAsync(Checkout(), cart, null);
        var second = await service.CreateAsync(Checkout(), cart, null);
        Assert.NotEqual(first.OrderNumber, second.OrderNumber);
        Assert.StartsWith("BISU-", first.OrderNumber);
    }

    [Fact]
    public async Task DashboardUsesLocalDatesAndExcludesCancelledOrders()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var day = new DateTime(2041, 6, 15);
        Order Sample(DateTime local, decimal total, OrderStatus status = OrderStatus.Pending) => new() {
            OrderNumber = Guid.NewGuid().ToString("N")[..25], CustomerName = "Ensayo KPI", CustomerEmail = "kpi@example.test", CustomerPhone = "2025550100",
            CreatedAt = SalesCriteria.Utc(local), Total = total, Subtotal = total, Status = status,
            Items = [new() { ProductId = f.ProductId, ProductName = "Pieza KPI", Quantity = 2, UnitPrice = total / 2 }]
        };
        db.AddRange(Sample(day.AddHours(23), 40), Sample(day.AddDays(-1).AddHours(23), 20), Sample(day.AddHours(12), 900, OrderStatus.Cancelled));
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminService>();
        var stats = await service.GetDashboardAsync(from: day, to: day);
        Assert.Equal(40, stats.PeriodSales); Assert.Equal(20, stats.PreviousSales);
        Assert.Equal(1, stats.PeriodOrders); Assert.Equal(40, stats.AverageTicket);
        Assert.Equal(40, stats.SalesByMonth.Sum(p => p.Value));
        Assert.Equal(40, stats.SalesByPayment.Sum(p => p.Value));
        Assert.Equal(40, stats.SalesByCategory.Sum(p => p.Value));
        Assert.Equal(2, stats.TopProducts.Sum(p => p.QuantitySold));
        var weekly = await service.GetDashboardAsync(from: day, to: day, interval: "week");
        Assert.Equal(stats.PeriodSales, weekly.SalesByMonth.Sum(p => p.Value));
    }
    [Theory]
    [InlineData("csv", "text/csv")][InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task DashboardExportsAreProtectedAndAvailable(string format, string contentType)
    {
        using var guest = f.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync("/Admin/Dashboard/Export?format=" + format)).StatusCode);
        using var admin = await f.Login("Admin");
        var response = await admin.GetAsync("/Admin/Dashboard/Export?format=" + format);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }
}

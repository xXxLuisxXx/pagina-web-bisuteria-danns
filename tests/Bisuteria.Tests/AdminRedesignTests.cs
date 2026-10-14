using System.Net;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Services;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task GlobalSearchFindsSkuAndRequiresAdmin()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sku = "SEARCH-" + Guid.NewGuid().ToString("N");
        var product = new Product { Name = "Búsqueda aislada", Slug = sku, Sku = sku, Price = 1, Stock = 5, CategoryId = f.CategoryId };
        db.Add(product); await db.SaveChangesAsync();
        try {
            using var admin = await f.Login("Admin");
            var result = await admin.GetAsync("/Admin/Search?q=" + sku);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains(sku, await result.Content.ReadAsStringAsync());
            using var guest = f.Client(); using var customer = await f.Login("Customer");
            Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync("/Admin/Search?q=" + sku)).StatusCode);
            Assert.Contains("AccessDenied", (await customer.GetAsync("/Admin/Search?q=" + sku)).Headers.Location!.OriginalString);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/Admin/Search?q=" + new string('x',201))).StatusCode);
        } finally { db.Remove(product); await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task CalendarAndMonthlyGoalUseUncancelledGuayaquilSales()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        var before = await settings.GetAsync("MonthlySalesGoal");
        await settings.SetAsync("MonthlySalesGoal", "100");
        var date = new DateTime(2045, 3, 4);
        var prefix = "CAL-" + Guid.NewGuid().ToString("N")[..12];
        var valid = new Order { OrderNumber = prefix, CreatedAt = SalesCriteria.Utc(date.AddHours(23)), Total = 40, CustomerEmail = "calendar@example.test" };
        var cancelled = new Order { OrderNumber = prefix + "-C", CreatedAt = valid.CreatedAt, Total = 900, Status = OrderStatus.Cancelled, CustomerEmail = "calendar@example.test" };
        db.AddRange(valid, cancelled); await db.SaveChangesAsync();
        try {
            var stats = await scope.ServiceProvider.GetRequiredService<IAdminService>().GetDashboardAsync(from:date,to:date);
            Assert.Equal(100, stats.MonthlyGoal); Assert.Equal(40, stats.CalendarSales);
            Assert.Equal(1, Assert.Single(stats.CalendarDays).Value);
            Assert.Equal("2045-03-04", stats.CalendarDays[0].Label);
        } finally { db.RemoveRange(valid,cancelled); await db.SaveChangesAsync(); await settings.SetAsync("MonthlySalesGoal", before); }
    }
}

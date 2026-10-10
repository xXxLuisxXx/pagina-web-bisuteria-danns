using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    public async Task CancellationBeforeShippingRestoresStockOnce(OrderStatus current)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var before = await db.Products.Where(p => p.Id == f.ProductId).Select(p => p.Stock).SingleAsync();
        var dto = await service.CreateAsync(Checkout(), new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        var order = await db.Orders.SingleAsync(o => o.Id == dto.Id);
        order.Status = current;
        await db.SaveChangesAsync();
        Assert.True(await service.UpdateStatusAsync(dto.Id, OrderStatus.Cancelled));
        Assert.True(await service.UpdateStatusAsync(dto.Id, OrderStatus.Cancelled));
        Assert.Equal(before, await db.Products.AsNoTracking().Where(p => p.Id == f.ProductId).Select(p => p.Stock).SingleAsync());
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    public async Task OrderTransitionsPreserveStock(OrderStatus current, OrderStatus next, bool allowed)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var dto = await service.CreateAsync(Checkout(), new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        var order = await db.Orders.SingleAsync(o => o.Id == dto.Id);
        order.Status = current;
        await db.SaveChangesAsync();
        var stock = await db.Products.Where(p => p.Id == f.ProductId).Select(p => p.Stock).SingleAsync();
        Assert.Equal(allowed, await service.UpdateStatusAsync(dto.Id, next));
        Assert.Equal(stock, await db.Products.AsNoTracking().Where(p => p.Id == f.ProductId).Select(p => p.Stock).SingleAsync());
        Assert.Equal(allowed ? next : current, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == dto.Id)).Status);
    }

    [Fact]
    public async Task PaidOrderRequiresRefundBeforeCancellationAndPreservesPaidAt()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var order = await service.CreateAsync(Checkout(), new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        Assert.True(await service.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Paid));
        var paidAt = (await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == order.Id)).PaidAt;
        Assert.NotNull(paidAt);
        Assert.True(await service.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Paid));
        Assert.Equal(paidAt, (await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == order.Id)).PaidAt);
        Assert.False(await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled));
        Assert.False(await service.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Pending));
        Assert.True(await service.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Refunded));
        Assert.Equal(paidAt, (await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == order.Id)).PaidAt);
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled));
        Assert.False(await service.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Paid));
    }
}

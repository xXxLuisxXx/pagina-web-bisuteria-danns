using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ConcurrentCheckoutTokenCreatesExactlyOneOrder()
    {
        var token = Guid.NewGuid();
        int productId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = new Product { Name = "Idempotencia", Slug = Guid.NewGuid().ToString("N"), CategoryId = f.CategoryId, Price = 20, Stock = 10, IsActive = true };
            db.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }
        async Task<int> Submit()
        {
            using var scope = f.Services.CreateScope();
            var checkout = Checkout();
            checkout.CheckoutToken = token;
            return (await scope.ServiceProvider.GetRequiredService<IOrderService>().CreateAsync(checkout,
                new CartDto { Items = [new CartItemDto { ProductId = productId, Quantity = 2 }] }, null)).Id;
        }
        var ids = await Task.WhenAll(Submit(), Submit());
        Assert.Equal(ids[0], ids[1]);
        using var verify = f.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.Orders.CountAsync(o => o.CheckoutToken == token));
        Assert.Equal(8, (await context.Products.SingleAsync(p => p.Id == productId)).Stock);
        Assert.Equal(1, await context.StockMovements.CountAsync(m => m.ProductId == productId));
    }
}

using System.Net;
using Bisuteria.Application.Validators;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Barrio León", true)]
    [InlineData("Barrio Leo\u0301n", true)]
    [InlineData("<script>alert(1)</script>", false)]
    [InlineData("Sector #1", false)]
    public void SectorInputValidatesEmptyAndUnsafeText(string sector, bool valid)
    {
        var checkout = Checkout();
        checkout.DeliverySector = sector;
        Assert.Equal(valid, new CheckoutValidator().Validate(checkout).IsValid);
    }

    [Fact]
    public void SectorNormalizationHandlesAccentsAndSpaces()
    {
        Assert.Equal("Barrio León", DeliverySectorRules.Clean("  Barrio   Leo\u0301n  "));
        Assert.Equal(DeliverySectorRules.Key("Barrio León"), DeliverySectorRules.Key("barrio   LEON"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CheckoutHttpResolvesSectorAndNeverTrustsPostedTariff(bool free, bool unknown)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var sector = "Barrio León " + suffix;
        if (!unknown)
        {
            db.DeliveryZones.Add(new DeliveryZone { Province = "Manabí", City = "Manta", Sector = sector, Cost = free ? 0m : 2.75m, IsFree = free, IsActive = true });
            await db.SaveChangesAsync();
        }
        using var client = f.Client();
        await AuditFactory.Post(client, "/Cart/Add", new() { ["productId"] = f.ProductId.ToString(), ["quantity"] = "1" });
        var email = suffix + "@example.test";
        var response = await AuditFactory.Post(client, "/Checkout", new()
        {
            ["CustomerName"] = "Cliente de prueba", ["CustomerEmail"] = email, ["CustomerPhone"] = "2025550100",
            ["City"] = "Manta", ["DeliverySector"] = sector.Replace("León", "leon"), ["AddressLine1"] = "Dirección ficticia",
            ["DeliveryZoneId"] = int.MaxValue.ToString(), ["ShippingCost"] = "0.01", ["ShippingToConfirm"] = "false",
            ["DeliveryMethod"] = "Shipping", ["PaymentMethod"] = "CashOnDelivery"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var order = await db.Orders.AsNoTracking().SingleAsync(item => item.CustomerEmail == email);
        Assert.Equal(unknown, order.ShippingToConfirm);
        Assert.Equal(unknown || free ? 0m : 2.75m, order.ShippingCost);
        Assert.Equal(order.Subtotal - order.Discount + order.ShippingCost, order.Total);
        Assert.Equal(unknown ? sector.Replace("León", "leon") : sector, order.DeliverySector);
        Assert.Equal("Manta", order.City);
    }

    [Fact]
    public async Task AmbiguousHistoricSectorsRequireQuote()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sector = "Zona León " + Guid.NewGuid().ToString("N");
        db.DeliveryZones.AddRange(
            new DeliveryZone { Province = "Manabí", City = "Manta", Sector = sector, Cost = 2m, IsActive = true },
            new DeliveryZone { Province = "Manabí", City = "Manta", Sector = sector.Replace("Zona ", "Zona  "), Cost = 4m, IsActive = true });
        await db.SaveChangesAsync();
        var checkout = Checkout();
        checkout.DeliverySector = sector;
        var order = await scope.ServiceProvider.GetRequiredService<Bisuteria.Application.Interfaces.IOrderService>()
            .CreateAsync(checkout, new() { Items = [new() { ProductId = f.ProductId, Quantity = 1 }] }, null);
        Assert.True(order.ShippingToConfirm);
        Assert.Equal(0m, order.ShippingCost);
    }
}

using System.ComponentModel.DataAnnotations;
using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Validators;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ShippingQuoteRejectsFractionsOfCentAndOutOfRangeAmounts()
    {
        using var scope = f.Services.CreateScope();
        var services = scope.ServiceProvider;
        var orders = services.GetRequiredService<IOrderService>();
        var checkout = new CheckoutDto
        {
            CustomerName = "Cliente de prueba", CustomerEmail = $"quote-{Guid.NewGuid():N}@example.test",
            CustomerPhone = "+593 999 111 222", AddressLine1 = "Calle de prueba",
            City = "Manta", DeliverySector = "Sector nuevo de prueba", DeliveryZoneId = f.DeliveryZoneId, PaymentMethod = PaymentMethod.CashOnDelivery, DeliveryMethod = DeliveryMethod.Shipping,
            CheckoutToken = Guid.NewGuid()
        };
        var order = await orders.CreateAsync(checkout,
            new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        var owner = await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("owner@example.test");
        Assert.NotNull(owner);

        Assert.False(await orders.ConfirmShippingAsync(order.Id, 1.001m, owner!.Id, StoreRoles.Owner));
        Assert.False(await orders.ConfirmShippingAsync(order.Id, 10000.01m, owner.Id, StoreRoles.Owner));

        var saved = await services.GetRequiredService<AppDbContext>().Orders.FindAsync(order.Id);
        Assert.NotNull(saved);
        Assert.True(saved!.ShippingToConfirm);
        Assert.Equal(0m, saved.ShippingCost);
    }

    [Theory]
    [InlineData("Manabí", true)]
    [InlineData(" manabí ", true)]
    [InlineData("Provincia inventada", false)]
    [InlineData("", true)]
    public void ProfileFormValidatesEcuadorianProvinces(string province, bool valid)
    {
        var profile = new ProfileViewModel { FirstName = "Cliente", LastName = "Prueba", Email = "customer@example.test", Province = province };
        Assert.Equal(valid, Validator.TryValidateObject(profile, new ValidationContext(profile), [], true));
    }
}

public class Phase7InputRuleTests
{
    [Theory]
    [InlineData("es-EC")]
    [InlineData("en-US")]
    public void DecimalAnnotationLimitsAreIndependentOfRequestCulture(string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            var product = new ProductInputDto { Name = "Producto de prueba", Price = 0.01m, ComparePrice = 2m, CategoryId = 1 };
            var coupon = new CouponInputDto { Code = "PRUEBA", Value = 0.01m };
            Assert.True(Validator.TryValidateObject(product, new ValidationContext(product), [], true));
            Assert.True(Validator.TryValidateObject(coupon, new ValidationContext(coupon), [], true));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1000000, 1000000, true)]
    [InlineData(1000001, 1, false)]
    [InlineData(1, 1000001, false)]
    public void StockBoundsMatchClientAndServer(int stock, int minimum, bool valid)
    {
        var product = new ProductInputDto { Name = "Producto de prueba", Price = 1m, CategoryId = 1, Stock = stock, MinStock = minimum };
        Assert.Equal(valid, new ProductInputValidator().Validate(product).IsValid);
        Assert.Equal(valid, Validator.TryValidateObject(product, new ValidationContext(product), [], true));
    }

    [Theory]
    [InlineData(2000, 2100, true)]
    [InlineData(1999, 2100, false)]
    [InlineData(2000, 2101, false)]
    public void CouponDatesStayWithinSupportedRange(int startYear, int endYear, bool valid)
    {
        var coupon = new CouponInputDto { Code = "PRUEBA", Value = 5m, StartsAt = new DateTime(startYear, 1, 1), ExpiresAt = new DateTime(endYear, 1, 1) };
        Assert.Equal(valid, new CouponInputValidator().Validate(coupon).IsValid);
    }

    [Theory]
    [InlineData(5, 200, 2000, true)]
    [InlineData(0, 200, 2000, false)]
    [InlineData(5, 201, 2000, false)]
    [InlineData(5, 200, 2001, false)]
    public void ReviewsPublishAnnotationBoundaries(int rating, int titleLength, int commentLength, bool valid)
    {
        var review = new CreateReviewDto { ProductId = 1, Rating = rating, Title = new string('a', titleLength), Comment = new string('a', commentLength) };
        Assert.Equal(valid, Validator.TryValidateObject(review, new ValidationContext(review), [], true));
    }
}

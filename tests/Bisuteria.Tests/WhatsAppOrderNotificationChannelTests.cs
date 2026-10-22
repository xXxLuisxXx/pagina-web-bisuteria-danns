using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Domain.Enums;
using Bisuteria.Web.Services;
using Xunit;

public sealed class WhatsAppOrderNotificationChannelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("1234567")]
    [InlineData("1234567890123456")]
    public void MissingOrInvalidStorePhoneDoesNotCreateExternalLink(string? phone)
        => Assert.Null(new WhatsAppOrderNotificationChannel().CreateWhatsAppUrl(Sample(), phone));

    [Fact]
    public void LinkContainsShortOrderSummaryWithoutEmailOrStreetAddress()
    {
        var order = Sample();
        var url = new WhatsAppOrderNotificationChannel().CreateWhatsAppUrl(order, "+593 999 000 111");

        Assert.StartsWith("https://wa.me/593999000111?text=", url);
        var message = Uri.UnescapeDataString(new Uri(url!).Query[6..]);
        Assert.Contains(order.OrderNumber, message);
        Assert.Contains("Pulsera", message);
        Assert.Contains("Personalización: talla mediana", message);
        Assert.Contains("sector de prueba, Manta", message);
        Assert.Contains("$12.50 USD", message);
        Assert.DoesNotContain(order.CustomerEmail, message);
        Assert.DoesNotContain(order.AddressLine1!, message);
        Assert.DoesNotContain("mensaje privado de regalo", message);
        Assert.True(message.Length <= 1800);
    }

    [Fact]
    public void LongProductDataIsCappedAndPendingShippingDoesNotInventTotal()
    {
        var order = Sample();
        order.Items = [new OrderItemDto { ProductName = new string('X', 4000), Quantity = 1 }];
        order.ShippingToConfirm = true;
        var url = new WhatsAppOrderNotificationChannel().CreateWhatsAppUrl(order, "593999000111");
        var message = Uri.UnescapeDataString(new Uri(url!).Query[6..]);

        Assert.True(message.Length <= 1800);
        Assert.Contains("Envío y total: pendientes de cotización.", message);
        Assert.DoesNotContain("$12.50 USD", message);
    }

    private static OrderDto Sample() => new()
    {
        OrderNumber = "BISU-PRUEBA", CustomerName = "Cliente de prueba", CustomerEmail = "private@example.test",
        CustomerPhone = "0999000111", AddressLine1 = "Calle privada 123", AddressLine2 = "Referencia privada",
        City = "Manta", Province = "Manabí", DeliverySector = "sector de prueba", DeliveryMethod = DeliveryMethod.Shipping,
        Total = 12.50m, IsGift = true, GiftMessage = "mensaje privado de regalo",
        Items = [new OrderItemDto { ProductName = "Pulsera", Quantity = 1, CustomizationSummary = "Personalización: talla mediana" }]
    };
}

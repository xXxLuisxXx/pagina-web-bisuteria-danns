using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.DTOs.Orders;

/// <summary>
/// Datos enviados en el checkout.
/// </summary>
public class CheckoutDto
{
    public Guid? CheckoutToken { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public string? Observations { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    public string? CouponCode { get; set; }

    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Shipping;

    public bool IsGift { get; set; }

    public string? GiftRecipientName { get; set; }

    public string? GiftMessage { get; set; }
}

public class OrderDto
{
    public string MainProductName { get; set; } = "";
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public string? Observations { get; set; }

    public OrderStatus Status { get; set; }

    public string StatusLabel => Status.GetLabel();

    public PaymentMethod PaymentMethod { get; set; }

    public string PaymentMethodLabel => PaymentMethod switch
    {
        PaymentMethod.CashOnDelivery => "Contra entrega",
        PaymentMethod.BankTransfer => "Transferencia bancaria",
        PaymentMethod.Card => "Tarjeta",
        _ => PaymentMethod.ToString()
    };

    public PaymentStatus PaymentStatus { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? TrackingNumber { get; set; }

    public DeliveryMethod DeliveryMethod { get; set; }

    public string DeliveryMethodLabel => DeliveryMethod.GetLabel();

    public bool IsGift { get; set; }

    public string? GiftRecipientName { get; set; }

    public string? GiftMessage { get; set; }

    public IReadOnlyList<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
}

public class OrderItemDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }

    public string? CustomizationSummary { get; set; }
}

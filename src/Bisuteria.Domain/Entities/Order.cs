using Bisuteria.Domain.Common;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Pedido realizado por un cliente (registrado o invitado).
/// </summary>
public class Order : BaseEntity
{
    public Guid? CheckoutToken { get; set; }
    public string? CouponCode { get; set; }
    /// <summary>Número legible del pedido, p. ej. "BISU-000123".</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Usuario registrado que realizó el pedido (null si es invitado).</summary>
    public string? UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public string? Observations { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal Total { get; set; }

    /// <summary>Número de seguimiento de envío (opcional).</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>Notas internas del administrador.</summary>
    public string? AdminNotes { get; set; }

    /// <summary>Método de entrega elegido por el cliente.</summary>
    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Shipping;

    /// <summary>Indica si el pedido es un regalo (empaque especial).</summary>
    public bool IsGift { get; set; }

    /// <summary>Nombre del destinatario del regalo (opcional).</summary>
    public string? GiftRecipientName { get; set; }

    /// <summary>Mensaje/nota personalizada para el regalo (opcional).</summary>
    public string? GiftMessage { get; set; }

    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

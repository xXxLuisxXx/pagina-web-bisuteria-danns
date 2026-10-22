using System.ComponentModel.DataAnnotations;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Web.Models;

public sealed class OrderTrackingViewModel
{
    [Required(ErrorMessage = "Escribe el número de pedido.")]
    [StringLength(40, ErrorMessage = "El número de pedido no es válido.")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe el correo usado en la compra.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    public OrderTrackingResultViewModel? Result { get; set; }
}

public sealed record OrderTrackingResultViewModel(
    string OrderNumber,
    DateTime CreatedAt,
    OrderStatus Status,
    string StatusLabel,
    DeliveryMethod DeliveryMethod,
    string DeliveryMethodLabel,
    string? DeliverySector,
    string? City,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingCost,
    decimal Total,
    bool ShippingToConfirm,
    IReadOnlyList<OrderTrackingItemViewModel> Items);

public sealed record OrderTrackingItemViewModel(string ProductName, int Quantity);

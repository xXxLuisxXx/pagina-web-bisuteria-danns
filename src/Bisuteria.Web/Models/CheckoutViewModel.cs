using System.ComponentModel.DataAnnotations;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bisuteria.Web.Models;

public class CheckoutViewModel
{
    public Guid? CheckoutToken { get; set; }
    [BindNever]
    public CartDto Cart { get; set; } = new();

    /// <summary>El recargo todavía debe ser confirmado por la tienda según el destino.</summary>
    [BindNever]
    public bool ShippingToConfirm { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [Display(Name = "Nombre completo")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Correo inválido.")]
    [Display(Name = "Correo electrónico")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [Phone(ErrorMessage = "Teléfono inválido.")]
    [Display(Name = "Teléfono")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Display(Name = "Dirección")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? AddressLine1 { get; set; }

    [Display(Name = "Referencia (opcional)")]
    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    [Display(Name = "Sector")]
    public int? DeliveryZoneId { get; set; }

    [BindNever]
    public IReadOnlyList<DeliveryZoneOption> DeliveryZones { get; set; } = Array.Empty<DeliveryZoneOption>();
    [BindNever]
    public decimal ShippingCost { get; set; }
    public bool CheckoutIntentRefreshZones { get; set; }

    [Display(Name = "Observaciones (opcional)")]
    public string? Observations { get; set; }

    [Display(Name = "Método de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    [Display(Name = "Método de entrega")]
    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Shipping;

    [Display(Name = "🎁 ¿Es un regalo?")]
    public bool IsGift { get; set; }

    [Display(Name = "Nombre del destinatario")]
    public string? GiftRecipientName { get; set; }

    [Display(Name = "Mensaje / nota de regalo")]
    public string? GiftMessage { get; set; }

    public string? CouponCode { get; set; }

    public CheckoutDto ToDto() => new()
    {
        CheckoutToken = CheckoutToken,
        CustomerName = CustomerName,
        CustomerEmail = CustomerEmail,
        CustomerPhone = CustomerPhone,
        AddressLine1 = AddressLine1 ?? string.Empty,
        AddressLine2 = AddressLine2,
        DeliveryZoneId = DeliveryZoneId,
        Observations = Observations,
        PaymentMethod = PaymentMethod,
        CouponCode = CouponCode,
        DeliveryMethod = DeliveryMethod,
        IsGift = IsGift,
        GiftRecipientName = GiftRecipientName,
        GiftMessage = GiftMessage
    };
}

public sealed record DeliveryZoneOption(int Id, string Province, string City, string Sector, decimal Cost,
    bool IsFree, bool RequiresConfirmation, string? EstimatedTime);

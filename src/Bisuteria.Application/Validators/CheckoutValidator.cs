using Bisuteria.Application.DTOs.Orders;
using FluentValidation;

namespace Bisuteria.Application.Validators;

/// <summary>
/// Validaciones completas del formulario de checkout.
/// </summary>
public class CheckoutValidator : AbstractValidator<CheckoutDto>
{
    public CheckoutValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(160).WithMessage("El nombre no puede superar 160 caracteres.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Ingresa un correo electrónico válido.")
            .MaximumLength(200).WithMessage("El correo no puede superar 200 caracteres.");

        RuleFor(x => x.CustomerPhone)
            .NotEmpty().WithMessage("El teléfono es obligatorio.")
            .Matches(@"^[0-9+\-\s()]{7,20}$").WithMessage("Ingresa un teléfono válido.")
            .MaximumLength(40).WithMessage("El teléfono no puede superar 40 caracteres.");

        RuleFor(x => x.AddressLine1)
            .NotEmpty().When(x => x.DeliveryMethod != Bisuteria.Domain.Enums.DeliveryMethod.Pickup).WithMessage("La dirección es obligatoria.")
            .MaximumLength(200).WithMessage("La dirección no puede superar 200 caracteres.");

        RuleFor(x => x.AddressLine2).MaximumLength(200).WithMessage("La dirección complementaria no puede superar 200 caracteres.");

        RuleFor(x => x.DeliveryZoneId)
            .NotNull().When(x => x.DeliveryMethod != Bisuteria.Domain.Enums.DeliveryMethod.Pickup)
            .WithMessage("Selecciona un sector de entrega.")
            .GreaterThan(0).When(x => x.DeliveryMethod != Bisuteria.Domain.Enums.DeliveryMethod.Pickup)
            .WithMessage("Selecciona un sector de entrega válido.");

        RuleFor(x => x.Observations)
            .MaximumLength(1000).WithMessage("Las observaciones no pueden superar 1000 caracteres.");

        RuleFor(x => x.DeliveryMethod).IsInEnum().WithMessage("Método de entrega no válido.");
        RuleFor(x => x.GiftRecipientName).MaximumLength(160).WithMessage("Nombre de destinatario demasiado largo.");
        RuleFor(x => x.GiftMessage).MaximumLength(300).WithMessage("El mensaje de regalo no puede superar 300 caracteres.");
        RuleFor(x => x.PaymentMethod)
            .IsInEnum().WithMessage("Método de pago no válido.");
    }
}

using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Domain.Enums;
using FluentValidation;
namespace Bisuteria.Application.Validators;

public class CouponInputValidator : AbstractValidator<CouponInputDto>
{
    public CouponInputValidator()
    {
        RuleFor(x => x.Value).Must(x => decimal.Round(x, 2) == x).WithMessage("El descuento admite hasta 2 decimales.");
        RuleFor(x => x.MinAmount).Must(x => decimal.Round(x, 2) == x).WithMessage("El importe mínimo admite hasta 2 decimales.");
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0).WithMessage("Identificador inválido.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40).Matches("^[a-zA-Z0-9_-]+$").WithMessage("Usa un código de hasta 40 letras, números, guiones o guiones bajos.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de descuento inválido.");
        RuleFor(x => x.Value).GreaterThan(0).LessThanOrEqualTo(9999999).WithMessage("El descuento debe ser positivo y estar dentro del límite permitido.");
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Type == DiscountType.Percentage).WithMessage("El porcentaje no puede superar 100.");
        RuleFor(x => x.MinAmount).InclusiveBetween(0, 9999999).WithMessage("Importe mínimo inválido.");
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses.HasValue).WithMessage("El límite de usos debe ser positivo.");
        RuleFor(x => x.ExpiresAt).GreaterThan(x => x.StartsAt).When(x => x.StartsAt.HasValue && x.ExpiresAt.HasValue).WithMessage("La fecha final debe ser posterior al inicio.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("La descripción no puede superar 1000 caracteres.");
    }
}
public class CustomizationInputValidator : AbstractValidator<CustomizationInputDto>
{
    public CustomizationInputValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, 50).WithMessage("Cantidad inválida.");
        RuleFor(x => x.Text).MaximumLength(200).WithMessage("El texto no puede superar 200 caracteres.");
        RuleFor(x => x.Initials).MaximumLength(10).WithMessage("Las iniciales no pueden superar 10 caracteres.");
        RuleFor(x => x.Date).MaximumLength(30).WithMessage("Fecha inválida.");
        RuleFor(x => x.Phrase).MaximumLength(300).WithMessage("La frase no puede superar 300 caracteres.");
        RuleFor(x => x.Color).MaximumLength(60).WithMessage("Color demasiado largo.");
        RuleFor(x => x.Font).MaximumLength(60).WithMessage("Fuente demasiado larga.");
        RuleFor(x => x.Charm).MaximumLength(120).WithMessage("Dije demasiado largo.");
        RuleFor(x => x.Material).MaximumLength(120).WithMessage("Material demasiado largo.");
        RuleFor(x => x.Size).MaximumLength(60).WithMessage("Talla demasiado larga.");
        RuleFor(x => x.Note).MaximumLength(1000).WithMessage("La nota no puede superar 1000 caracteres.");
    }
}
public class GalleryImageValidator : AbstractValidator<GalleryImageDto>
{
    public GalleryImageValidator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0).WithMessage("Identificador inválido.");
        RuleFor(x => x.ImageUrl).Must(InputValidation.IsImageUrl).When(x => !string.IsNullOrWhiteSpace(x.ImageUrl)).WithMessage("Sube una imagen o usa una ruta local que empiece por /img/.");
        RuleFor(x => x.Title).MaximumLength(200).WithMessage("Título demasiado largo.");
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Descripción demasiado larga.");
        RuleFor(x => x.Category).MaximumLength(80).WithMessage("La categoría no puede superar 80 caracteres.");
    }
}

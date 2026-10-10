using Bisuteria.Application.DTOs.Catalog;
using FluentValidation;

namespace Bisuteria.Application.Validators;

/// <summary>
/// Validaciones para el registro/edición de productos.
/// </summary>
public class ProductInputValidator : AbstractValidator<ProductInputDto>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("El precio debe ser mayor que cero.")
            .LessThanOrEqualTo(9999999).WithMessage("El precio es demasiado alto.");

        RuleFor(x => x.ComparePrice)
            .Must((product, price) => !price.HasValue || price > product.Price)
            .WithMessage("El precio anterior debe ser mayor que el precio de venta.");
        RuleFor(x => x.Price).Must(p => decimal.Round(p, 2) == p).WithMessage("El precio admite hasta 2 decimales.");
        RuleFor(x => x.ComparePrice).Must(p => !p.HasValue || (p <= 9999999 && decimal.Round(p.Value, 2) == p))
            .WithMessage("El precio anterior admite hasta 2 decimales y debe estar dentro del rango permitido.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock no puede ser negativo.");

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock mínimo no puede ser negativo.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Selecciona una categoría.");

        RuleFor(x => x.Id).GreaterThanOrEqualTo(0).WithMessage("Identificador inválido.");
        RuleFor(x => x.Description).MaximumLength(10000).WithMessage("La descripción no puede superar 10000 caracteres.");
        RuleFor(x => x.Size).MaximumLength(60).WithMessage("El tamaño no puede superar 60 caracteres.");
        RuleFor(x => x.Sku)
            .MaximumLength(60).WithMessage("El SKU no puede superar 60 caracteres.");

        RuleFor(x => x.Colors)
            .MaximumLength(300).WithMessage("Los colores no pueden superar 300 caracteres.");

        RuleFor(x => x.Material)
            .MaximumLength(120).WithMessage("El material no puede superar 120 caracteres.");
    }
}

/// <summary>
/// Validaciones para categorías.
/// </summary>
public class CategoryInputValidator : AbstractValidator<CategoryInputDto>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0).WithMessage("Identificador inválido.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage("El orden no puede ser negativo.");
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("La descripción no puede superar 2000 caracteres.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar 120 caracteres.");
    }
}

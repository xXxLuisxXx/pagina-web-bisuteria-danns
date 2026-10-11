using Bisuteria.Application.DTOs.Common;
using FluentValidation;

namespace Bisuteria.Application.Validators;

public sealed class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10000).WithMessage("La página debe estar entre 1 y 10000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 48).WithMessage("El tamaño de página debe estar entre 1 y 48.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("La búsqueda admite hasta 200 caracteres.");
        RuleFor(x => x.CategorySlug).MaximumLength(200).WithMessage("La categoría admite hasta 200 caracteres.");
        RuleFor(x => x.MinPrice).Must(ValidMoney).WithMessage("El precio mínimo debe estar entre 0 y 9999999, con hasta 2 decimales.");
        RuleFor(x => x.MaxPrice).Must(ValidMoney).WithMessage("El precio máximo debe estar entre 0 y 9999999, con hasta 2 decimales.");
        RuleFor(x => x.MaxPrice).Must((q, max) => !max.HasValue || !q.MinPrice.HasValue || max >= q.MinPrice).WithMessage("El precio máximo no puede ser menor que el mínimo.");
        RuleFor(x => x.Sort).Must(s => s is null or "relevance" or "price_asc" or "price_desc" or "newest" or "name_asc" or "bestsellers").WithMessage("El orden solicitado no es válido.");
    }
    private static bool ValidMoney(decimal? value) => !value.HasValue || value >= 0 && value <= 9999999 && decimal.Round(value.Value, 2) == value;
}

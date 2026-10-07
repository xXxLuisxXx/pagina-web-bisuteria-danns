using Bisuteria.Application.DTOs.Catalog;
using FluentValidation;

namespace Bisuteria.Application.Validators;

/// <summary>
/// Validaciones para las reseñas de clientes.
/// </summary>
public class CreateReviewValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("Producto no válido.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("La calificación debe estar entre 1 y 5 estrellas.");

        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Escribe tu comentario.")
            .MaximumLength(2000).WithMessage("El comentario no puede superar 2000 caracteres.");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("El título no puede superar 200 caracteres.");
    }
}

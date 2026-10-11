using Bisuteria.Application.DTOs.Blog;
using FluentValidation;

namespace Bisuteria.Application.Validators;

/// <summary>
/// Validaciones para publicaciones del blog.
/// </summary>
public class BlogPostValidator : AbstractValidator<BlogPostInputDto>
{
    public BlogPostValidator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0).WithMessage("Identificador inválido.");
        RuleFor(x => x.Tags).MaximumLength(300).WithMessage("Las etiquetas admiten hasta 300 caracteres.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(220).WithMessage("El título no puede superar 220 caracteres.");

        RuleFor(x => x.Excerpt)
            .NotEmpty().WithMessage("El resumen es obligatorio.")
            .MaximumLength(400).WithMessage("El resumen no puede superar 400 caracteres.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("El contenido es obligatorio.")
            .MaximumLength(20000).WithMessage("El contenido admite hasta 20000 caracteres.");

        RuleFor(x => x.Author)
            .MaximumLength(120).WithMessage("El autor no puede superar 120 caracteres.");
    }
}

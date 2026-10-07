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
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(220).WithMessage("El título no puede superar 220 caracteres.");

        RuleFor(x => x.Excerpt)
            .NotEmpty().WithMessage("El resumen es obligatorio.")
            .MaximumLength(400).WithMessage("El resumen no puede superar 400 caracteres.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("El contenido es obligatorio.");

        RuleFor(x => x.Author)
            .MaximumLength(120).WithMessage("El autor no puede superar 120 caracteres.");
    }
}

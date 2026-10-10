using FluentValidation;
namespace Bisuteria.Application.Validators;
public static class InputValidation
{
    public static void Check<T>(IValidator<T> validator, T value)
    {
        var result = validator.Validate(value);
        if (!result.IsValid)
            throw new Bisuteria.Application.Services.BusinessRuleException(string.Join(" ", result.Errors.Select(x => x.ErrorMessage).Distinct()));
    }

    public static bool IsImageUrl(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 &&
        value.StartsWith("/img/", StringComparison.Ordinal) && !value.Contains("..") && !value.Contains('\\') && !value.Contains('%');
}

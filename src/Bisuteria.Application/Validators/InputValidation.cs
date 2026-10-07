using FluentValidation;
namespace Bisuteria.Application.Validators;
public static class InputValidation
{
    public static void Check<T>(IValidator<T> validator, T value)
    {
        var result = validator.Validate(value);
        if (!result.IsValid)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.ErrorMessage).Distinct()));
    }

    public static bool IsImageUrl(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 &&
        ((value.StartsWith("/img/", StringComparison.Ordinal) && !value.Contains("..") && !value.Contains('\\')) ||
         (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)));
}

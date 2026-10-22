using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bisuteria.Application.Validators;

public static class DeliverySectorRules
{
    public const int MaxLength = 60;

    // Compatible con los validadores de .NET y jQuery, también al pegar tildes descompuestas.
    public const string Pattern = @"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\u0300-\u036F0-9 .,'’()\-]+$";

    public static string Clean(string? value) => Regex.Replace(
        (value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ");

    public static string Key(string? value) => string.Concat(Clean(value).Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC).ToUpperInvariant();
}

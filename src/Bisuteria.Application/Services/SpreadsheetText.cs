namespace Bisuteria.Application.Services;

public static class SpreadsheetText
{
    public static string Safe(string? value)
    {
        value ??= "";
        var first = value.TrimStart().FirstOrDefault();
        return first is '=' or '+' or '-' or '@' || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n')
            ? "'" + value : value;
    }
}

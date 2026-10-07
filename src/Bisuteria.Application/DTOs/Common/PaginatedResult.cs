namespace Bisuteria.Application.DTOs.Common;

/// <summary>
/// Resultado paginado genérico.
/// </summary>
public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Criterios de búsqueda/filtrado del catálogo.
/// </summary>
public class ProductQuery
{
    public string? Search { get; set; }

    public string? CategorySlug { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? Sort { get; set; } = "relevance";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public bool OnlyActive { get; set; } = true;
}

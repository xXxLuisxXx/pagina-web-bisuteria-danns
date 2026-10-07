using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Common;

namespace Bisuteria.Web.Models;

/// <summary>Modelo de la página de catálogo con filtros.</summary>
public class CatalogViewModel
{
    public PaginatedResult<ProductCardDto> Products { get; set; } = new();

    public List<CategoryDto> Categories { get; set; } = new();

    public ProductQuery Query { get; set; } = new();

    public Dictionary<int, bool> Favorites { get; set; } = new();

    public decimal? MaxPriceBound { get; set; } = 100m;
}

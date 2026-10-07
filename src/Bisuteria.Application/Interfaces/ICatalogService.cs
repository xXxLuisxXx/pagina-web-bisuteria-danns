using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Common;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de catálogo: productos, categorías, búsqueda, favoritos y galería.
/// </summary>
public interface ICatalogService
{
    Task<PaginatedResult<ProductCardDto>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetProductAsync(int id, CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<List<CategoryDto>> GetCategoriesAsync(bool onlyActive = true, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetFeaturedAsync(int count = 8, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetNewProductsAsync(int count = 8, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetBestSellersAsync(int count = 8, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetCustomizableAsync(int count = 8, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetRelatedAsync(int productId, int count = 4, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    Task<List<string>> SearchSuggestionsAsync(string term, int count = 8, CancellationToken cancellationToken = default);

    Task<(decimal MinPrice, decimal MaxPrice)> GetPriceRangeAsync(CancellationToken cancellationToken = default);

    Task<List<GalleryImageDto>> GetGalleryAsync(string? category = null, CancellationToken cancellationToken = default);

    Task<Dictionary<int, bool>> GetFavoritesAsync(string userId, IEnumerable<int> productIds, CancellationToken cancellationToken = default);

    Task ToggleFavoriteAsync(string userId, int productId, CancellationToken cancellationToken = default);

    Task<List<ProductCardDto>> GetUserFavoritesAsync(string userId, CancellationToken cancellationToken = default);

    Task IncrementProductViewsAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Valores de comparación de un producto para la vista "Comparar".</summary>
}

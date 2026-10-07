using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Common;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de catálogo.
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly IUnitOfWork _unitOfWork;

    public CatalogService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResult<ProductCardDto>> GetProductsAsync(
        ProductQuery query, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Product>();
        IQueryable<Product> q = repo.Query()
            .Include(p => p.Category)
            .Include(p => p.Images);

        if (query.OnlyActive)
            q = q.Where(p => p.IsActive && p.Category != null && p.Category.IsActive);

        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
            q = q.Where(p => p.Category != null && p.Category.Slug == query.CategorySlug);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(term) ||
                             (p.Description != null && p.Description.Contains(term)) ||
                             (p.Material != null && p.Material.Contains(term)) ||
                             (p.Colors != null && p.Colors.Contains(term)));
        }

        if (query.MinPrice.HasValue)
            q = q.Where(p => (p.ComparePrice.HasValue && p.ComparePrice.Value > 0 && p.ComparePrice.Value < p.Price ? p.ComparePrice.Value : p.Price) >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            q = q.Where(p => (p.ComparePrice.HasValue && p.ComparePrice.Value > 0 && p.ComparePrice.Value < p.Price ? p.ComparePrice.Value : p.Price) <= query.MaxPrice.Value);

        q = query.Sort switch
        {
            "price_asc" => q.OrderBy(p => (p.ComparePrice.HasValue && p.ComparePrice.Value > 0 && p.ComparePrice.Value < p.Price ? p.ComparePrice.Value : p.Price)),
            "price_desc" => q.OrderByDescending(p => (p.ComparePrice.HasValue && p.ComparePrice.Value > 0 && p.ComparePrice.Value < p.Price ? p.ComparePrice.Value : p.Price)),
            "name_asc" => q.OrderBy(p => p.Name),
            "newest" => q.OrderByDescending(p => p.CreatedAt),
            "bestsellers" => q.OrderByDescending(p => p.SoldCount),
            _ => q.OrderBy(p => p.Name)
        };

        var total = await q.CountAsync(ct);
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)Math.Clamp(query.PageSize, 1, 48))));
        var pageSize = Math.Clamp(query.PageSize, 1, 48);

        var products = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = products.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);

        return new PaginatedResult<ProductCardDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductDetailDto?> GetProductAsync(int id, CancellationToken ct = default)
    {
        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && p.Category != null && p.Category.IsActive, ct);

        if (product is null)
            return null;

        var dto = EntityMappings.ToDetail(product);
        var reviews = await _unitOfWork.Repository<Review>().Query()
            .Where(r => r.ProductId == id && r.IsApproved)
            .OrderByDescending(r => r.ReviewDate)
            .Select(EntityMappings.ReviewProjection)
            .ToListAsync(ct);
        dto.ApprovedReviews = reviews;
        dto.AverageRating = reviews.Count == 0 ? 0 : reviews.Average(r => r.Rating);
        dto.ReviewCount = reviews.Count;
        return dto;
    }

    public async Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);
        return product is null ? null : await GetProductAsync(product.Id, ct);
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(bool onlyActive = true, CancellationToken ct = default)
    {
        var categories = await _unitOfWork.Repository<Category>().Query()
            .Include(c => c.Products.Where(p => p.IsActive && p.Category != null && p.Category.IsActive))
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return categories
            .Where(c => !onlyActive || c.IsActive)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                IsActive = c.IsActive,
                DisplayOrder = c.DisplayOrder,
                ProductCount = c.Products.Count
            })
            .ToList();
    }

    public async Task<List<ProductCardDto>> GetFeaturedAsync(int count = 8, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive && p.IsFeatured)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .OrderByDescending(p => p.SoldCount)
            .Take(count)
            .ToListAsync(ct);
        var dtos = items.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task<List<ProductCardDto>> GetNewProductsAsync(int count = 8, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive && p.IsNew)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
        var dtos = items.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task<List<ProductCardDto>> GetBestSellersAsync(int count = 8, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .OrderByDescending(p => p.SoldCount)
            .ThenByDescending(p => p.Views)
            .Take(count)
            .ToListAsync(ct);
        var dtos = items.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task<List<ProductCardDto>> GetCustomizableAsync(int count = 8, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive && p.IsCustomizable)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .OrderByDescending(p => p.SoldCount)
            .Take(count)
            .ToListAsync(ct);
        var dtos = items.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task<List<ProductCardDto>> GetRelatedAsync(int productId, int count = 4, CancellationToken ct = default)
    {
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId, ct);
        if (product is null)
            return new List<ProductCardDto>();

        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive && p.Id != productId &&
                        (p.CategoryId == product.CategoryId || p.Material == product.Material))
            .Include(p => p.Category)
            .Include(p => p.Images)
            .OrderByDescending(p => p.SoldCount)
            .Take(count)
            .ToListAsync(ct);
        var dtos = items.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task<List<ProductCardDto>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return new List<ProductCardDto>();

        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => idList.Contains(p.Id) && p.IsActive && p.Category != null && p.Category.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .ToListAsync(ct);

        var byId = items.ToDictionary(p => p.Id);
        var dtos = idList
            .Where(id => byId.ContainsKey(id))
            .Select(id => EntityMappings.ToCard(byId[id])).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task<List<string>> SearchSuggestionsAsync(string term, int count = 8, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
            return new List<string>();

        var t = term.Trim();
        var names = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive && p.Name.Contains(t))
            .Select(p => p.Name)
            .Distinct()
            .Take(count)
            .ToListAsync(ct);
        return names;
    }

    public async Task<(decimal MinPrice, decimal MaxPrice)> GetPriceRangeAsync(CancellationToken ct = default)
    {
        var products = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
            .Select(p => (p.ComparePrice.HasValue && p.ComparePrice.Value > 0 && p.ComparePrice.Value < p.Price ? p.ComparePrice.Value : p.Price))
            .ToListAsync(ct);

        return products.Count == 0
            ? (0m, 100m)
            : (products.Min(), products.Max());
    }

    public async Task<List<GalleryImageDto>> GetGalleryAsync(string? category = null, CancellationToken ct = default)
    {
        IQueryable<GalleryImage> q = _unitOfWork.Repository<GalleryImage>().Query()
            .OrderByDescending(g => g.CreatedAt);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(g => g.Category == category);

        return await q.Select(EntityMappings.GalleryProjection).ToListAsync(ct);
    }

    public async Task<Dictionary<int, bool>> GetFavoritesAsync(string userId, IEnumerable<int> productIds, CancellationToken ct = default)
    {
        var ids = productIds.Distinct().ToList();
        if (string.IsNullOrEmpty(userId) || ids.Count == 0)
            return new Dictionary<int, bool>();

        var favs = await _unitOfWork.Repository<Favorite>().Query()
            .Where(f => f.UserId == userId && ids.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(ct);

        return ids.ToDictionary(id => id, id => favs.Contains(id));
    }

    public async Task ToggleFavoriteAsync(string userId, int productId, CancellationToken ct = default)
    {
        if (!await _unitOfWork.Repository<Product>().AnyAsync(p => p.Id == productId && p.IsActive, ct))
            return;
        var repo = _unitOfWork.Repository<Favorite>();
        var existing = await repo.FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);
        if (existing is not null)
            repo.Remove(existing);
        else
            await repo.AddAsync(new Favorite { UserId = userId, ProductId = productId }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<ProductCardDto>> GetUserFavoritesAsync(string userId, CancellationToken ct = default)
    {
        var favoriteIds = _unitOfWork.Repository<Favorite>().Query().Where(f => f.UserId == userId).Select(f => f.ProductId);
        var items = await _unitOfWork.Repository<Product>().Query()
            .Where(p => favoriteIds.Contains(p.Id) && p.IsActive && p.Category != null && p.Category.IsActive)
            .Include(p => p.Category).Include(p => p.Images)
            .OrderByDescending(p => p.Id).ToListAsync(ct);
        var dtos = items.Select(EntityMappings.ToCard).ToList();
        await AttachRatingsAsync(dtos, ct);
        return dtos;
    }

    public async Task IncrementProductViewsAsync(int productId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Product>();
        var product = await repo.GetByIdAsync(productId, ct);
        if (product is null)
            return;
        product.Views++;
        repo.Update(product);
        await _unitOfWork.SaveChangesAsync(ct);
    }


    /// <summary>
    /// Adjunta el promedio de calificación y número de reseñas a cada tarjeta.
    /// </summary>
    private async Task AttachRatingsAsync(List<ProductCardDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0)
            return;

        var ids = dtos.Select(d => d.Id).ToList();
        var reviews = await _unitOfWork.Repository<Review>().Query()
            .Where(r => r.IsApproved && ids.Contains(r.ProductId))
            .GroupBy(r => r.ProductId)
            .Select(g => new { ProductId = g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
            .ToListAsync(ct);

        var lookup = reviews.ToDictionary(r => r.ProductId);
        foreach (var dto in dtos)
        {
            if (lookup.TryGetValue(dto.Id, out var rating))
            {
                dto.AverageRating = rating.Avg;
                dto.ReviewCount = rating.Count;
            }
        }
    }
}

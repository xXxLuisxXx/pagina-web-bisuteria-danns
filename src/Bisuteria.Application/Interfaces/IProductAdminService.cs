using Bisuteria.Application.DTOs.Catalog;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de gestión del catálogo para el panel administrativo.
/// </summary>
public interface IProductAdminService
{
    Task<ProductInputDto?> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    Task<int> SaveAsync(ProductInputDto dto, IEnumerable<string> newImageUrls, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<List<CategoryDto>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default);

    Task<int> SaveCategoryAsync(CategoryInputDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
}

using Bisuteria.Application.DTOs.Catalog;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Administración de la galería de imágenes del sitio.
/// </summary>
public interface IGalleryAdminService
{
    Task<List<GalleryImageDto>> GetAllAsync(CancellationToken ct = default);
    Task<GalleryImageDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(GalleryImageDto dto, CancellationToken ct = default);
    Task UpdateAsync(GalleryImageDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

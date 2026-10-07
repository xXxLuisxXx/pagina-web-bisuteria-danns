using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Imagen de la galería del sitio (diseño tipo Pinterest).
/// </summary>
public class GalleryImage : BaseEntity
{
    public string ImageUrl { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Categoría visual para el filtro de la galería.</summary>
    public string? Category { get; set; }

    public bool IsFeatured { get; set; }
}

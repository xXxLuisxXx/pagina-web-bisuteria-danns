using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Imagen asociada a un producto. Un producto puede tener varias imágenes.
/// </summary>
public class ProductImage : BaseEntity
{
    public int ProductId { get; set; }

    public virtual Product? Product { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public bool IsPrimary { get; set; }

    public int DisplayOrder { get; set; }
}

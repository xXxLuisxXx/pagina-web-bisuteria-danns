using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Opinión o reseña de un cliente sobre un producto.
/// </summary>
public class Review : BaseEntity
{
    public string? UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    /// <summary>SKU del producto usado únicamente durante el seed para vincular la reseña.</summary>
    public string? ReviewSku { get; set; }

    /// <summary>Fotografía del cliente que publica la opinión.</summary>
    public string? CustomerPhotoUrl { get; set; }

    public int ProductId { get; set; }

    public virtual Product? Product { get; set; }

    public int Rating { get; set; } = 5;

    public string? Title { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>Las reseñas deben ser aprobadas antes de publicarse.</summary>
    public bool IsApproved { get; set; }

    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
}

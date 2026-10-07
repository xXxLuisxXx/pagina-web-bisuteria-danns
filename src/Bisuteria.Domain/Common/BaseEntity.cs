namespace Bisuteria.Domain.Common;

/// <summary>
/// Clase base para todas las entidades persistentes del sistema.
/// Proporciona identidad y marcas de tiempo de auditoría.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

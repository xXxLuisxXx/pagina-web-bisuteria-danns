using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Favorito: producto guardado por un usuario para referencia futura.
/// </summary>
public class Favorite : BaseEntity
{
    public string UserId { get; set; } = string.Empty;

    public int ProductId { get; set; }

    public virtual Product? Product { get; set; }
}

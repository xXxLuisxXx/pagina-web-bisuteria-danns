using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Producto del catálogo de bisutería.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Precio de venta actual.</summary>
    public decimal Price { get; set; }

    /// <summary>Precio de referencia (antes de descuento). Null si no hay promoción.</summary>
    public decimal? ComparePrice { get; set; }

    /// <summary>Material principal de la pieza (acero, resina, etc.).</summary>
    public string? Material { get; set; }

    /// <summary>Lista de colores disponibles separados por coma.</summary>
    public string? Colors { get; set; }

    /// <summary>Talla disponible de la pieza.</summary>
    public string? Size { get; set; }

    /// <summary>Código único de inventario.</summary>
    public string? Sku { get; set; }

    /// <summary>Cantidad disponible en inventario.</summary>
    public int Stock { get; set; }

    /// <summary>Stock mínimo para alertas de reposición.</summary>
    public int MinStock { get; set; } = 5;

    /// <summary>Total de unidades vendidas (contador para "más vendidos").</summary>
    public int SoldCount { get; set; }

    /// <summary>Cantidad de visualizaciones del detalle.</summary>
    public int Views { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsFeatured { get; set; }

    /// <summary>Se muestra en la sección de "nuevos productos".</summary>
    public bool IsNew { get; set; }

    public bool IsCustomizable { get; set; } = true;

    public int CategoryId { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    /// <summary>Imagen principal para tarjetas y listados.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? MainImageUrl => Images.OrderBy(i => i.IsPrimary ? 0 : 1)
                                         .ThenBy(i => i.DisplayOrder)
                                         .FirstOrDefault()?.ImageUrl;

    /// <summary>Precio final (considera compare price si aplica).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public decimal EffectivePrice => ComparePrice.HasValue && ComparePrice.Value > 0 && ComparePrice.Value < Price
        ? ComparePrice.Value
        : Price;
}

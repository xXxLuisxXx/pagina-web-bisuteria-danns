namespace Bisuteria.Application.DTOs.Catalog;

/// <summary>Tarjeta compacta de producto (listados, destacados, relacionados).</summary>
public class ProductCardDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    public decimal? ComparePrice { get; set; }

    public decimal EffectivePrice { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsNew { get; set; }

    public bool IsCustomizable { get; set; }

    public string? Material { get; set; }

    public string? Colors { get; set; }

    public string? Size { get; set; }

    public string? MainImageUrl { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int SoldCount { get; set; }

    public bool IsInStock => Stock > 0;

    public double AverageRating { get; set; }

    public int ReviewCount { get; set; }
}

/// <summary>Detalle completo de producto.</summary>
public class ProductDetailDto : ProductCardDto
{
    public string? ComparePriceText { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string? MinStock { get; set; }

    public int Views { get; set; }

    public string? CategorySlug { get; set; }

    public IReadOnlyList<ProductImageDto> Images { get; set; } = Array.Empty<ProductImageDto>();

    public IReadOnlyList<ReviewDto> ApprovedReviews { get; set; } = Array.Empty<ReviewDto>();
}

public class ProductImageDto
{
    public int Id { get; set; }
    [System.ComponentModel.DataAnnotations.Display(Name = "URL de imagen")]
    public string ImageUrl { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public bool IsPrimary { get; set; }
}

public class ProductInputDto
{
    public List<ProductImageDto> Images { get; set; } = new();
    public List<int> RemoveImageIds { get; set; } = new();
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "{0} admite hasta 200 caracteres.")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "{0} es obligatorio.")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    [System.ComponentModel.DataAnnotations.StringLength(10000, ErrorMessage = "{0} admite hasta 10000 caracteres.")]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "9999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "{0} debe estar entre 0.01 y 9999999.")]
    public decimal Price { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "9999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "{0} debe estar entre 0.01 y 9999999.")]
    public decimal? ComparePrice { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(120, ErrorMessage = "{0} admite hasta 120 caracteres.")]
    public string? Material { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(300, ErrorMessage = "{0} admite hasta 300 caracteres.")]
    public string? Colors { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(60, ErrorMessage = "{0} admite hasta 60 caracteres.")]
    public string? Size { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(60, ErrorMessage = "{0} admite hasta 60 caracteres.")]
    public string? Sku { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(int), "0", "1000000", ErrorMessage = "{0} debe estar entre 0 y 1000000.")]
    public int Stock { get; set; }

    public int? OriginalStock { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(int), "0", "1000000", ErrorMessage = "{0} debe estar entre 0 y 1000000.")]
    public int MinStock { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public bool IsFeatured { get; set; }

    public bool IsNew { get; set; }

    public bool IsCustomizable { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Range(typeof(int), "1", "2147483647", ErrorMessage = "{0} debe estar entre 1 y 2147483647.")]
    public int CategoryId { get; set; }
}

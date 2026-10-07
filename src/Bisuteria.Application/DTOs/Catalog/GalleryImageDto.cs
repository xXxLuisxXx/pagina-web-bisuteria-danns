namespace Bisuteria.Application.DTOs.Catalog;

public class GalleryImageDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "URL de imagen")]
    public string ImageUrl { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    public string? Title { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Categoría")]
    public string? Category { get; set; }

    public bool IsFeatured { get; set; }
}

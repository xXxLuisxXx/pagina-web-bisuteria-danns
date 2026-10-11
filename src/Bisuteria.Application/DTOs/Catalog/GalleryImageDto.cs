namespace Bisuteria.Application.DTOs.Catalog;

public class GalleryImageDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "URL de imagen")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "{0} admite hasta 500 caracteres.")]
    public string ImageUrl { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "{0} admite hasta 200 caracteres.")]
    public string? Title { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    [System.ComponentModel.DataAnnotations.StringLength(2000, ErrorMessage = "{0} admite hasta 2000 caracteres.")]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Categoría")]
    [System.ComponentModel.DataAnnotations.StringLength(80, ErrorMessage = "{0} admite hasta 80 caracteres.")]
    public string? Category { get; set; }

    public bool IsFeatured { get; set; }
}

namespace Bisuteria.Application.DTOs.Catalog;

public class CategoryDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "URL de imagen")]
    public string? ImageUrl { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Orden de presentación")]
    public int DisplayOrder { get; set; }

    public int ProductCount { get; set; }

    public bool IsActive { get; set; }
}

public class CategoryInputDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    [System.ComponentModel.DataAnnotations.StringLength(120, ErrorMessage = "{0} admite hasta 120 caracteres.")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "{0} es obligatorio.")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    [System.ComponentModel.DataAnnotations.StringLength(2000, ErrorMessage = "{0} admite hasta 2000 caracteres.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Display(Name = "Orden de presentación")]
    [System.ComponentModel.DataAnnotations.Range(typeof(int), "0", "1000000", ErrorMessage = "{0} debe estar entre 0 y 1000000.")]
    public int DisplayOrder { get; set; }
}

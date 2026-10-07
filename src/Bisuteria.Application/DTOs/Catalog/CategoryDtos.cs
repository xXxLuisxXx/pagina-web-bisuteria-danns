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
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Display(Name = "Orden de presentación")]
    public int DisplayOrder { get; set; }
}

namespace Bisuteria.Application.DTOs.Blog;

public class BlogPostDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Resumen")]
    public string Excerpt { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Contenido")]
    public string Content { get; set; } = string.Empty;

    public string? CoverImage { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Autor")]
    public string? Author { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Etiquetas")]
    public string? Tags { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int Views { get; set; }
}

public class BlogPostInputDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Resumen")]
    public string Excerpt { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Contenido")]
    public string Content { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Autor")]
    public string? Author { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Etiquetas")]
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = true;
}

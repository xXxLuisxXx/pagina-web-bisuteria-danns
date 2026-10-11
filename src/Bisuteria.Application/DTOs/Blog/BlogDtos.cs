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
    [System.ComponentModel.DataAnnotations.StringLength(220, ErrorMessage = "{0} admite hasta 220 caracteres.")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "{0} es obligatorio.")]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Resumen")]
    [System.ComponentModel.DataAnnotations.StringLength(400, ErrorMessage = "{0} admite hasta 400 caracteres.")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "{0} es obligatorio.")]
    public string Excerpt { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Contenido")]
    [System.ComponentModel.DataAnnotations.StringLength(20000, ErrorMessage = "{0} admite hasta 20000 caracteres.")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "{0} es obligatorio.")]
    public string Content { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Autor")]
    [System.ComponentModel.DataAnnotations.StringLength(120, ErrorMessage = "{0} admite hasta 120 caracteres.")]
    public string? Author { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Etiquetas")]
    [System.ComponentModel.DataAnnotations.StringLength(300, ErrorMessage = "{0} admite hasta 300 caracteres.")]
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = true;
}

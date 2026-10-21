namespace Bisuteria.Application.DTOs.Catalog;

public class ReviewDto
{
    public int Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerPhotoUrl { get; set; }

    public int Rating { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    public string? Title { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime ReviewDate { get; set; }
}

public class CreateReviewDto
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "Producto no válido.")]
    public int ProductId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 5, ErrorMessage = "La calificación debe estar entre 1 y 5 estrellas.")]
    public int Rating { get; set; } = 5;

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "El título no puede superar 200 caracteres.")]
    public string? Title { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Escribe tu comentario.")]
    [System.ComponentModel.DataAnnotations.StringLength(2000, MinimumLength = 1, ErrorMessage = "El comentario no puede superar 2000 caracteres.")]
    public string Comment { get; set; } = string.Empty;
}

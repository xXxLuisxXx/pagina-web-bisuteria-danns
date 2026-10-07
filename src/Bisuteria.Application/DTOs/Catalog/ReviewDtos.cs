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
    public int ProductId { get; set; }

    public int Rating { get; set; } = 5;

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    public string? Title { get; set; }

    public string Comment { get; set; } = string.Empty;
}

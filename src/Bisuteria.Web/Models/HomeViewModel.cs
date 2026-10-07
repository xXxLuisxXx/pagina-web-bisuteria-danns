using Bisuteria.Application.DTOs.Blog;
using Bisuteria.Application.DTOs.Catalog;

namespace Bisuteria.Web.Models;

/// <summary>Modelo de la página de inicio.</summary>
public class HomeViewModel
{
    public List<CategoryDto> Categories { get; set; } = new();

    public List<ProductCardDto> Featured { get; set; } = new();

    public List<ProductCardDto> NewArrivals { get; set; } = new();

    public List<ProductCardDto> BestSellers { get; set; } = new();

    public List<BlogPostDto> LatestPosts { get; set; } = new();

    public List<ProductCardDto> CustomizableProducts { get; set; } = new();
}

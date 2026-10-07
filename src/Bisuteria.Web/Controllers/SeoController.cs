using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

/// <summary>
/// Genera el sitemap.xml dinámico con las páginas públicas y los productos activos.
/// </summary>
public class SeoController : Controller
{
    private readonly ICatalogService _catalog;
    private readonly IBlogService _blog;

    public SeoController(ICatalogService catalog, IBlogService blog)
    {
        _catalog = catalog;
        _blog = blog;
    }

    [HttpGet]
    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var urls = new List<string>
        {
            baseUrl,
            $"{baseUrl}/Catalog",
            $"{baseUrl}/Customizer",
            $"{baseUrl}/Gallery",
            $"{baseUrl}/Home/About",
            $"{baseUrl}/Blog",
            $"{baseUrl}/Contact"
        };

        var page = 1;
        Bisuteria.Application.DTOs.Common.PaginatedResult<Bisuteria.Application.DTOs.Catalog.ProductCardDto> products;
        do
        {
            products = await _catalog.GetProductsAsync(new Bisuteria.Application.DTOs.Common.ProductQuery { Page = page++, PageSize = 48, OnlyActive = true }, ct);
            foreach (var product in products.Items)
                urls.Add(baseUrl + Url.Action("Details", "Catalog", new { slug = product.Slug }));
        } while (page <= products.TotalPages);

        var posts = await _blog.GetPublishedAsync(50, ct);
        foreach (var post in posts)
            urls.Add(baseUrl + Url.Action("Details", "Blog", new { slug = post.Slug }));

        var xml = new System.Text.StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var url in urls.Distinct())
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{System.Security.SecurityElement.Escape(url)}</loc>");
            xml.AppendLine("  </url>");
        }
        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml");
    }
}

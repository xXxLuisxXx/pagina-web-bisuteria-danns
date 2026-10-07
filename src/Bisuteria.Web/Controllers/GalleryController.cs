using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class GalleryController : Controller
{
    private readonly ICatalogService _catalog;

    public GalleryController(ICatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? category = null, CancellationToken ct = default)
    {
        ViewData["Title"] = "Galería";
        var images = await _catalog.GetGalleryAsync(category, ct);
        var categories = images.Select(i => i.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        ViewBag.Categories = categories;
        ViewBag.ActiveCategory = category;
        return View(images);
    }
}

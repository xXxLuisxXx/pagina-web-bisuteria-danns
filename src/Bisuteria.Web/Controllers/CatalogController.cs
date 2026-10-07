using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Common;
using Bisuteria.Application.Interfaces;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class CatalogController : Controller
{
    private readonly ICatalogService _catalog;
    private readonly IReviewService _reviews;

    public CatalogController(ICatalogService catalog, IReviewService reviews)
    {
        _catalog = catalog;
        _reviews = reviews;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ProductQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Catálogo";

        query.OnlyActive = true;
        var products = await _catalog.GetProductsAsync(query, ct);

        var favorites = new Dictionary<int, bool>();
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var ids = products.Items.Select(p => p.Id).ToList();
            favorites = await _catalog.GetFavoritesAsync(userId, ids, ct);
        }

        var model = new CatalogViewModel
        {
            Products = products,
            Categories = await _catalog.GetCategoriesAsync(true, ct),
            Query = query,
            Favorites = favorites,
            MaxPriceBound = (await _catalog.GetPriceRangeAsync(ct)).MaxPrice
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string? slug, int? id, CancellationToken ct)
    {
        ProductDetailDto? product;
        if (!string.IsNullOrWhiteSpace(slug))
            product = await _catalog.GetProductBySlugAsync(slug, ct);
        else if (id.HasValue)
            product = await _catalog.GetProductAsync(id.Value, ct);
        else
            return NotFound();

        if (product is null || !product.IsActive)
            return NotFound();

        await _catalog.IncrementProductViewsAsync(product.Id, ct);
        ViewData["Title"] = product.Name;

        var related = await _catalog.GetRelatedAsync(product.Id, 4, ct);
        bool isFavorite = false;
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var map = await _catalog.GetFavoritesAsync(userId, new[] { product.Id }, ct);
            isFavorite = map.GetValueOrDefault(product.Id);
        }

        ViewBag.Related = related;
        ViewBag.IsFavorite = isFavorite;
        return View(product);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(CreateReviewDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var product = await _catalog.GetProductAsync(dto.ProductId, ct);
            if (product is null)
                return NotFound();
            ViewData["Title"] = product.Name;
            ViewBag.Related = await _catalog.GetRelatedAsync(product.Id, 4, ct);
            return View("Details", product);
        }

        string? customerName = null;
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
            {
            var manager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Bisuteria.Infrastructure.Identity.ApplicationUser>>();
            customerName = (await manager.GetUserAsync(User))?.FirstName ?? "Cliente";
        }

        var ok = await _reviews.CreateAsync(dto, userId, customerName, ct);
        TempData[ok ? "Success" : "Error"] = ok
            ? "Gracias por tu reseña. Será publicada tras la aprobación del equipo."
            : "Ya enviaste una reseña para este producto.";

        return RedirectToAction(nameof(Details), new { id = dto.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite([FromBody] ToggleFavoriteRequest request, CancellationToken ct)
    {
        if (request == null || request.ProductId <= 0)
            return Json(new { success = false, message = "Solicitud inválida." });

        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Json(new { success = false, requiresLogin = true });

        await _catalog.ToggleFavoriteAsync(userId, request.ProductId, ct);
        var map = await _catalog.GetFavoritesAsync(userId, new[] { request.ProductId }, ct);
        return Json(new { success = true, isFavorite = map.GetValueOrDefault(request.ProductId) });
    }

    [HttpGet]
    public async Task<IActionResult> SearchSuggestions(string term, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Json(Array.Empty<string>());
        var suggestions = await _catalog.SearchSuggestionsAsync(term, 8, ct);
        return Json(suggestions);
    }

    private string? GetUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}

public class ToggleFavoriteRequest
{
    public int ProductId { get; set; }
}

using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class CustomizerController : Controller
{
    private readonly ICatalogService _catalog;

    public CustomizerController(ICatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? productId, CancellationToken ct)
    {
        ViewData["Title"] = "Personalizador";

        var baseProducts = await _catalog.GetCustomizableAsync(24, ct);
        ViewBag.BaseProducts = baseProducts;

        if (productId.HasValue)
        {
            var product = await _catalog.GetProductAsync(productId.Value, ct);
            if (product is not null && product.IsActive && product.IsCustomizable)
            {
                ViewBag.SelectedProduct = product;
            }
        }

        return View();
    }
}

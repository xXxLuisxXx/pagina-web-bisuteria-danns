using System.Diagnostics;
using Bisuteria.Application.Interfaces;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class HomeController : Controller
{
    private readonly ICatalogService _catalog;
    private readonly IBlogService _blog;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ICatalogService catalog, IBlogService blog, ILogger<HomeController> logger)
    {
        _catalog = catalog;
        _blog = blog;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new HomeViewModel
        {
            Categories = await _catalog.GetCategoriesAsync(true, ct),
            Featured = await _catalog.GetFeaturedAsync(8, ct),
            NewArrivals = await _catalog.GetNewProductsAsync(8, ct),
            BestSellers = await _catalog.GetBestSellersAsync(8, ct),
            LatestPosts = await _blog.GetPublishedAsync(3, ct),
            CustomizableProducts = await _catalog.GetCustomizableAsync(4, ct)
        };

        // Sin título explícito: el layout genera "Bisuterias Dann's | Bisutería personalizada hecha a mano"
        return View(model);
    }

    public IActionResult About()
    {
        ViewData["Title"] = "Nuestra Historia";
        return View();
    }

    public IActionResult Faq()
    {
        ViewData["Title"] = "Preguntas frecuentes";
        return View();
    }

    public IActionResult Shipping()
    {
        ViewData["Title"] = "Envíos y devoluciones";
        return View();
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "Política de privacidad";
        return View();
    }

    [Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return View("Error", new ErrorViewModel { StatusCode = code });
    }

    [Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using Bisuteria.Application.Interfaces;
using Bisuteria.Application.DTOs.Common;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.InventoryAdjust)]
public class InventoryController : AdminBaseController
{
    private readonly IAdminService _admin;
    private readonly ICatalogService _catalog;

    public InventoryController(IAdminService admin, ICatalogService catalog)
    {
        _admin = admin;
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Inventario";
        ViewBag.LowStock = await _admin.GetLowStockAsync(ct);
        ViewBag.Movements = await _admin.GetStockMovementsAsync(limit: 50, cancellationToken: ct);
        var all = new List<Bisuteria.Application.DTOs.Catalog.ProductCardDto>();
        var page = 1;
        PaginatedResult<Bisuteria.Application.DTOs.Catalog.ProductCardDto> batch;
        do
        {
            batch = await _catalog.GetProductsAsync(new ProductQuery { Page = page++, PageSize = 48, OnlyActive = false }, ct);
            all.AddRange(batch.Items);
        } while (page <= batch.TotalPages);
        ViewBag.Products = new PaginatedResult<Bisuteria.Application.DTOs.Catalog.ProductCardDto> { Items = all, TotalCount = all.Count, Page = 1, PageSize = Math.Max(1, all.Count) };
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStock(int productId, int quantity, string reason, CancellationToken ct)
    {
        var result = await _admin.AddStockAsync(productId, quantity, reason, User.Identity?.Name, ct);
        TempData[result >= 0 ? "Success" : "Error"] = result >= 0
            ? $"Stock agregado. Nuevo stock: {result}."
            : "No se pudo agregar stock. Verifica el producto y la cantidad.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStock(int productId, int quantity, string reason, CancellationToken ct)
    {
        var result = await _admin.RemoveStockAsync(productId, quantity, reason, User.Identity?.Name, ct);
        TempData[result >= 0 ? "Success" : "Error"] = result >= 0
            ? $"Stock descontado. Nuevo stock: {result}."
            : "No se pudo descontar stock. Verifica el producto y la cantidad.";
        return RedirectToAction(nameof(Index));
    }
}

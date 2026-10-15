using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.CategoriesManage)]
public class CategoriesController : AdminBaseController
{
    private readonly IProductAdminService _products;
    private readonly ICatalogService _catalog;

    public CategoriesController(IProductAdminService products, ICatalogService catalog)
    {
        _products = products;
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Categorías";
        var categories = await _catalog.GetCategoriesAsync(onlyActive: false, ct);
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nueva categoría";
        return View("Edit", new CategoryInputDto());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Editar categoría";
        var categories = await _catalog.GetCategoriesAsync(onlyActive: false, ct);
        var category = categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
            return NotFound();

        return View(new CategoryInputDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(dto);

        try { await _products.SaveCategoryAsync(dto, ct); }
        catch (Bisuteria.Application.Services.BusinessRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(dto);
        }
        TempData["Success"] = "Categoría guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _products.DeleteCategoryAsync(id, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Categoría eliminada." : "No se puede eliminar una categoría con productos.";
        return RedirectToAction(nameof(Index));
    }
}

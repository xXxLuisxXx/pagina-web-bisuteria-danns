using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Common;
using Bisuteria.Application.Interfaces;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class ProductsController : AdminBaseController
{
    private readonly IProductAdminService _products;
    private readonly ICatalogService _catalog;
    private readonly IImageUploadService _imageUpload;

    public ProductsController(IProductAdminService products, ICatalogService catalog, IImageUploadService imageUpload)
    {
        _products = products;
        _catalog = catalog;
        _imageUpload = imageUpload;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Productos";
        var result = await _catalog.GetProductsAsync(new ProductQuery
        {
            Search = search,
            Page = page,
            PageSize = 15,
            OnlyActive = false
        }, ct);
        ViewBag.Search = search;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        var model = id.HasValue ? await _products.GetForEditAsync(id.Value, ct) : new ProductInputDto();
        if (id.HasValue && model is null)
            return NotFound();

        ViewBag.Categories = new SelectList(await _products.GetCategoryOptionsAsync(ct), "Id", "Name", model!.CategoryId);
        ViewData["Title"] = id.HasValue ? "Editar producto" : "Nuevo producto";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductInputDto dto, string[] newImages, List<IFormFile>? imageFiles, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            dto.Images = dto.Id > 0 ? (await _products.GetForEditAsync(dto.Id, ct))?.Images ?? new() : new();
            ViewBag.Categories = new SelectList(await _products.GetCategoryOptionsAsync(ct), "Id", "Name", dto.CategoryId);
            return View(dto);
        }

        var uploadedUrls = new List<string>();
        var saved = false;
        try
        {
            uploadedUrls = await _imageUpload.SaveManyAsync(imageFiles ?? new List<IFormFile>(), "products", ct);
            var allImages = (newImages ?? Array.Empty<string>()).Concat(uploadedUrls).ToArray();

            var id = await _products.SaveAsync(dto, allImages, ct);
            saved = true;
            TempData["Success"] = uploadedUrls.Count > 0
                ? $"Producto guardado correctamente ({uploadedUrls.Count} imagen(es) optimizada(s))."
                : "Producto guardado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            dto.Images = dto.Id > 0 ? (await _products.GetForEditAsync(dto.Id, ct))?.Images ?? new() : new();
            ViewBag.Categories = new SelectList(await _products.GetCategoryOptionsAsync(ct), "Id", "Name", dto.CategoryId);
            return View(dto);
        }
        finally
        {
            if (!saved) _imageUpload.DiscardNewUploads(uploadedUrls);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        TempData["Success"] = "Producto retirado del catálogo. Se conserva el historial cuando corresponde.";
        return RedirectToAction(nameof(Index));
    }
}

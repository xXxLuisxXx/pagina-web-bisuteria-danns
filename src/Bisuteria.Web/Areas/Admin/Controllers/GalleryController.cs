using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.Interfaces;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.GalleryManage)]
public class GalleryController : AdminBaseController
{
    private readonly IGalleryAdminService _gallery;
    private readonly IImageUploadService _imageUpload;

    public GalleryController(IGalleryAdminService gallery, IImageUploadService imageUpload)
    {
        _gallery = gallery;
        _imageUpload = imageUpload;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Galería";
        var images = await _gallery.GetAllAsync(ct);
        return View(images);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        ViewData["Title"] = id.HasValue ? "Editar imagen" : "Nueva imagen";
        var model = id.HasValue ? await _gallery.GetByIdAsync(id.Value, ct) : new GalleryImageDto();
        if (id.HasValue && model is null)
            return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GalleryImageDto dto, IFormFile? imageFile, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(dto);

        string? uploadedUrl = null;
        var saved = false;
        try
        {
            if (imageFile is not null && imageFile.Length > 0)
            {
                uploadedUrl = await _imageUpload.SaveOptimizedAsync(imageFile, "gallery", ct);
                dto.ImageUrl = uploadedUrl;
            }
            if (string.IsNullOrWhiteSpace(dto.ImageUrl))
                throw new Bisuteria.Application.Services.BusinessRuleException("Debes subir una imagen o indicar una URL.");
            if (dto.Id == 0) await _gallery.CreateAsync(dto, ct);
            else await _gallery.UpdateAsync(dto, ct);
            saved = true;
            TempData["Success"] = "Imagen guardada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Bisuteria.Application.Services.BusinessRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(dto);
        }
        finally
        {
            if (!saved && uploadedUrl is not null) _imageUpload.DiscardNewUploads([uploadedUrl]);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _gallery.DeleteAsync(id, ct);
        TempData["Success"] = "Imagen eliminada.";
        return RedirectToAction(nameof(Index));
    }
}

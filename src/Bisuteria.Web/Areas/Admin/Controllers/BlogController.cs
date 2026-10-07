using Bisuteria.Application.DTOs.Blog;
using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class BlogController : AdminBaseController
{
    private readonly IBlogService _blog;

    public BlogController(IBlogService blog)
    {
        _blog = blog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Blog";
        var posts = await _blog.GetPagedAsync(page, 15, publishedOnly: false, cancellationToken: ct);
        return View(posts);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nueva publicación";
        return View("Edit", new BlogPostInputDto());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Editar publicación";
        var dto = await _blog.GetForEditAsync(id, ct);
        return dto is null ? NotFound() : View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BlogPostInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(dto);

        try { await _blog.SaveAsync(dto, ct); }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(dto);
        }
        TempData["Success"] = "Publicación guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _blog.DeleteAsync(id, ct);
        TempData["Success"] = "Publicación eliminada.";
        return RedirectToAction(nameof(Index));
    }

}

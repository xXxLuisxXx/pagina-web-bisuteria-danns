using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class BlogController : Controller
{
    private readonly IBlogService _blog;

    public BlogController(IBlogService blog)
    {
        _blog = blog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? search = null, CancellationToken ct = default)
    {
        ViewData["Title"] = "Blog · Consejos y tendencias";
        var result = await _blog.GetPagedAsync(page, 9, publishedOnly: true, search: search, ct);
        ViewBag.Search = search;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var post = await _blog.GetBySlugAsync(slug, ct);
        if (post is null)
            return NotFound();

        await _blog.IncrementViewsAsync(post.Id, ct);
        ViewData["Title"] = post.Title;
        ViewBag.Related = await _blog.GetPublishedAsync(3, ct);
        return View(post);
    }
}

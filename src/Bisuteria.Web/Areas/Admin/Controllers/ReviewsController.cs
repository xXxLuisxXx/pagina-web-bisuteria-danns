using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.ReviewsModerate)]
public class ReviewsController : AdminBaseController
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews)
    {
        _reviews = reviews;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool pendingOnly = false, CancellationToken ct = default)
    {
        ViewData["Title"] = "Reseñas";
        ViewBag.PendingOnly = pendingOnly;
        ViewBag.PendingCount = await _reviews.CountPendingAsync(ct);
        var reviews = await _reviews.GetAdminListAsync(pendingOnly, ct);
        return View(reviews);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        if (!await _reviews.ApproveAsync(id, ct)) return NotFound();
        TempData["Success"] = "Reseña aprobada y publicada.";
        return RedirectToAction(nameof(Index), new { pendingOnly = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!await _reviews.DeleteAsync(id, ct)) return NotFound();
        TempData["Success"] = "Reseña eliminada.";
        return RedirectToAction(nameof(Index));
    }
}

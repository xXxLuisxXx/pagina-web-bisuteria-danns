using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.CouponsManage)]
public class CouponsController : AdminBaseController
{
    private readonly ICouponAdminService _coupons;

    public CouponsController(ICouponAdminService coupons)
    {
        _coupons = coupons;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Cupones";
        var coupons = await _coupons.GetListAsync(ct);
        return View(coupons);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nuevo cupón";
        return View("Edit", new CouponInputDto());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Editar cupón";
        var dto = await _coupons.GetForEditAsync(id, ct);
        if (dto is null)
            return NotFound();
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CouponInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(dto);

        try
        {
            await _coupons.SaveAsync(dto, ct);
            TempData["Success"] = "Cupón guardado.";
            return RedirectToAction(nameof(Index));
        }
        catch (Bisuteria.Application.Services.BusinessRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
    {
        if (!await _coupons.ToggleActiveAsync(id, ct)) return NotFound();
        TempData["Success"] = "Cupón actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!await _coupons.DeleteAsync(id, ct)) return NotFound();
        TempData["Success"] = "Cupón eliminado.";
        return RedirectToAction(nameof(Index));
    }
}

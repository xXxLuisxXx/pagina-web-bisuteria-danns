using Bisuteria.Application.Services;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Web.Cart;
using Bisuteria.Web.Models;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class CartController : Controller
{
    private readonly CartManager _cart;
    private readonly ICatalogService _catalog;
    private readonly CartPreparationService _preparation;

    public CartController(CartManager cart, ICatalogService catalog, CartPreparationService preparation)
    {
        _cart = cart;
        _catalog = catalog;
        _preparation = preparation;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Carrito de compras";
        var cart = await _cart.GetCartAsync(ct);

        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(CartAddViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Verifica el producto y la cantidad (entre 1 y 50).";
            return RedirectToAction(nameof(Index));
        }
        var product = await _catalog.GetProductAsync(model.ProductId, ct);
        if (product is null || !product.IsActive)
            return NotFound();

        int? customizationId;
        try
        {
            customizationId = await _preparation.PrepareAsync(model.ProductId, model.Quantity,
                _cart.Items.Where(i => i.ProductId == model.ProductId).Sum(i => i.Quantity),
                model.Customization, HttpContext.Session.Id, ct);
        }
        catch (BusinessRuleException ex) { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Index)); }

        await _cart.AddAsync(model.ProductId, model.Quantity, customizationId, ct);

        TempData["Success"] = customizationId.HasValue
            ? "¡Personalización añadida al carrito! ✨"
            : "Producto añadido al carrito.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int productId, int quantity, int? customizationId)
    {
        if (!ModelState.IsValid) return BadRequest("Cantidad inválida.");
        var product = await _catalog.GetProductAsync(productId);
        var others = _cart.Items.Where(i => i.ProductId == productId && i.CustomizationId != customizationId).Sum(i => i.Quantity);
        if (product is null || quantity < 1 || quantity + others > Math.Min(50, product.Stock))
        {
            TempData["Error"] = "La cantidad supera las existencias disponibles o el límite de 50 unidades.";
            return RedirectToAction(nameof(Index));
        }
        await _cart.UpdateQuantityAsync(productId, quantity, customizationId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, int? customizationId)
    {
        await _cart.RemoveAsync(productId, customizationId);
        TempData["Success"] = "Producto eliminado del carrito.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "Ingresa un código de cupón.";
            return RedirectToAction(nameof(Index));
        }

        var cart = await _cart.GetCartAsync(ct);
        var validation = await _cart.ValidateCouponAsync(code, cart.Subtotal, ct);
        if (validation.IsValid)
        {
            await _cart.ApplyCouponAsync(code);
            TempData["Success"] = $"¡Cupón aplicado! Descuento de {validation.Discount.ToString("C")}.";
        }
        else
        {
            TempData["Error"] = validation.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoupon()
    {
        await _cart.RemoveCouponAsync();
        TempData["Success"] = "Cupón eliminado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Clear()
    {
        await _cart.ClearAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Count()
    {
        var count = await _cart.CountAsync();
        return Json(new { count });
    }
}

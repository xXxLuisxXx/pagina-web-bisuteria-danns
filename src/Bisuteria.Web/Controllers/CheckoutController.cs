using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Bisuteria.Web.Cart;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class CheckoutController : Controller
{
    private readonly CartManager _cart;
    private readonly IOrderService _orders;
    private readonly IConfiguration _configuration;
    private PaymentMethod[] AvailablePayments => (_configuration.GetSection("Payments:EnabledMethods").Get<string[]>() ?? [])
        .Select(x => Enum.TryParse<PaymentMethod>(x, out var method) && method is PaymentMethod.CashOnDelivery or PaymentMethod.BankTransfer ? (PaymentMethod?)method : null)
        .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();

    public CheckoutController(CartManager cart, IOrderService orders, IConfiguration configuration)
    {
        _cart = cart;
        _orders = orders;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Finalizar compra";
        ViewBag.AvailablePayments = AvailablePayments;
        var cart = await _cart.GetCartAsync(ct);
        if (cart.IsEmpty)
            return RedirectToAction(nameof(CartController.Index), "Cart");

        var model = new CheckoutViewModel
        {
            Cart = cart,
            CouponCode = cart.CouponCode,
            PaymentMethod = AvailablePayments.FirstOrDefault()
        };

        // Pre-rellenar datos del cliente autenticado.
        if (User.Identity?.IsAuthenticated == true)
        {
            model.CustomerName = User.Identity.Name ?? string.Empty;
            model.CustomerEmail = User.Identity.Name ?? string.Empty;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Finalizar compra";
        ViewBag.AvailablePayments = AvailablePayments;
        var cart = await _cart.GetCartAsync(ct);
        model.Cart = cart;

        if (cart.IsEmpty)
            return RedirectToAction(nameof(CartController.Index), "Cart");

        var validation = new Bisuteria.Application.Validators.CheckoutValidator().Validate(model.ToDto());
        foreach (var error in validation.Errors) ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        if (!AvailablePayments.Contains(model.PaymentMethod))
            ModelState.AddModelError(string.Empty, "No hay un método de pago habilitado para esta solicitud. Contacta con la tienda.");
        if (!ModelState.IsValid)
            return View(model);

        model.CouponCode = cart.CouponCode;
        var dto = model.ToDto();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        OrderDto order;
        try
        {
            order = await _orders.CreateAsync(dto, cart, userId, ct);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        await _cart.ClearAsync();

        TempData["OrderNumber"] = order.OrderNumber;
        HttpContext.Session.SetString("Bisuteria.LastOrderNumber", order.OrderNumber);
        return RedirectToAction(nameof(Confirmation), new { orderNumber = order.OrderNumber });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(string orderNumber, CancellationToken ct)
    {
        ViewData["Title"] = "Pedido confirmado";
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (order is null)
            return NotFound();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrWhiteSpace(userId) && order.UserId == userId;
        var isCurrentGuestOrder = string.Equals(HttpContext.Session.GetString("Bisuteria.LastOrderNumber"), orderNumber, StringComparison.Ordinal);
        if (!User.IsInRole("Admin") && !isOwner && !isCurrentGuestOrder)
            return Challenge();

        return View(order);
    }

    [HttpGet]
    public IActionResult ThankYou() => RedirectToAction(nameof(Index));
}

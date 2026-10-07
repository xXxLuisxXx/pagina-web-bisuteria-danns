using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderService _orders;

    public OrderController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Mis pedidos";
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction(nameof(AccountController.Login), "Account");

        var orders = await _orders.GetByUserAsync(userId, ct);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string orderNumber, CancellationToken ct)
    {
        ViewData["Title"] = "Detalle del pedido";
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var order = await _orders.GetByNumberAsync(orderNumber, ct);

        if (order is null || (order.UserId != userId && !User.IsInRole("Admin")))
            return NotFound();

        return View(order);
    }
}

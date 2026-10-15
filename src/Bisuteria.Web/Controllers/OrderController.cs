using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bisuteria.Domain.Security;

namespace Bisuteria.Web.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderService _orders;
    private readonly IAuthorizationService _authorization;

    public OrderController(IOrderService orders, IAuthorizationService authorization)
    {
        _orders = orders;
        _authorization = authorization;
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

        var canViewAll = (await _authorization.AuthorizeAsync(User, null, StorePermissions.OrdersView)).Succeeded;
        if (order is null || (order.UserId != userId && !canViewAll))
            return NotFound();

        return View(order);
    }
}

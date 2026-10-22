using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bisuteria.Domain.Security;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.RateLimiting;

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

    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Track()
    {
        ViewData["Title"] = "Seguimiento de pedido";
        ViewData["Robots"] = "noindex, nofollow";
        return View(new OrderTrackingViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-forms")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Track(OrderTrackingViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Seguimiento de pedido";
        ViewData["Robots"] = "noindex, nofollow";
        if (!ModelState.IsValid) return View(model);

        var order = await _orders.GetByNumberAsync(model.OrderNumber.Trim(), ct);
        if (order is null || order.UserId is not null ||
            !string.Equals(order.CustomerEmail, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "No encontramos un pedido de invitado con esos datos. Revisa el número y el correo de la compra.");
            return View(model);
        }

        model.OrderNumber = order.OrderNumber;
        model.Result = new OrderTrackingResultViewModel(
            order.OrderNumber, order.CreatedAt, order.Status, order.StatusLabel,
            order.DeliveryMethod, order.DeliveryMethodLabel, order.DeliverySector, order.City,
            order.Subtotal, order.Discount, order.ShippingCost, order.Total, order.ShippingToConfirm,
            order.Items.Select(item => new OrderTrackingItemViewModel(item.ProductName, item.Quantity)).ToArray());
        return View(model);
    }
}

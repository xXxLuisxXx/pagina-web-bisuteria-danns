using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class OrdersController : AdminBaseController
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpGet]
    public async Task<IActionResult> Index(OrderStatus? status = null, string? search = null, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Pedidos";
        var result = await _orders.GetPagedAsync(page, 15, status, search, ct);
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Statuses = Enum.GetValues<OrderStatus>();
        ViewBag.StatusCounts = new Dictionary<OrderStatus, int>
        {
            [OrderStatus.Pending] = await _orders.CountByStatusAsync(OrderStatus.Pending, ct),
            [OrderStatus.Preparing] = await _orders.CountByStatusAsync(OrderStatus.Preparing, ct),
            [OrderStatus.Shipped] = await _orders.CountByStatusAsync(OrderStatus.Shipped, ct),
            [OrderStatus.Delivered] = await _orders.CountByStatusAsync(OrderStatus.Delivered, ct),
            [OrderStatus.Cancelled] = await _orders.CountByStatusAsync(OrderStatus.Cancelled, ct)
        };
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string orderNumber, CancellationToken ct)
    {
        ViewData["Title"] = "Detalle del pedido";
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (order is null)
            return NotFound();

        ViewBag.Statuses = Enum.GetValues<OrderStatus>();
        ViewBag.AdminNotes = await _orders.GetAdminNotesAsync(order.Id, ct);
        ViewBag.PaymentStatuses = Enum.GetValues<PaymentStatus>();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string orderNumber, OrderStatus status, string? adminNotes, CancellationToken ct)
    {
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (!ModelState.IsValid || order is null || !await _orders.UpdateStatusAsync(order.Id, status, adminNotes, ct))
        {
            TempData["Error"] = "El estado solicitado no es válido o el pedido está cancelado.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Estado del pedido actualizado.";
        return RedirectToAction(nameof(Details), new { orderNumber });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePayment(string orderNumber, PaymentStatus status, CancellationToken ct)
    {
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (!ModelState.IsValid || order is null || !await _orders.UpdatePaymentStatusAsync(order.Id, status, ct))
        {
            TempData["Error"] = "El estado solicitado no es válido o el pedido está cancelado.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Estado del pago actualizado.";
        return RedirectToAction(nameof(Details), new { orderNumber });
    }
}

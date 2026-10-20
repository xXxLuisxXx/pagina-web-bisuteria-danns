using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.OrdersView)]
public class OrdersController : AdminBaseController
{
    private readonly IOrderService _orders;
    private readonly IPaymentProofService _proofs;
    private readonly Bisuteria.Web.Services.IPaymentProofStorage _proofStorage;
    private readonly IAuthorizationService _authorization;

    public OrdersController(IOrderService orders, IPaymentProofService proofs, Bisuteria.Web.Services.IPaymentProofStorage proofStorage,
        IAuthorizationService authorization)
    {
        _orders = orders;
        _proofs = proofs;
        _proofStorage = proofStorage;
        _authorization = authorization;
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

        ViewBag.Statuses = Enum.GetValues<OrderStatus>().Where(s => OrderTransitions.Allows(order.Status, s, order.PaymentStatus)).ToArray();
        ViewBag.AdminNotes = await _orders.GetAdminNotesAsync(order.Id, ct);
        ViewBag.PaymentStatuses = Enum.GetValues<PaymentStatus>().Where(s => OrderTransitions.AllowsPayment(order.Status, order.PaymentStatus, s)).ToArray();
        var proofs = await _proofs.GetForOrderAsync(orderNumber, ct);
        ViewBag.PaymentProofs = proofs;
        ViewBag.CanEditShipping = order.DeliveryMethod != DeliveryMethod.Pickup && order.PaymentStatus == PaymentStatus.Pending &&
            proofs.All(proof => proof.Status == PaymentProofStatus.Rejected) &&
            (await _authorization.AuthorizeAsync(User, null, StorePermissions.OrdersSetShipping)).Succeeded;
        ViewBag.CanViewProof = (await _authorization.AuthorizeAsync(User, null, StorePermissions.PaymentsViewProof)).Succeeded;
        ViewBag.CanReviewProof = (await _authorization.AuthorizeAsync(User, null, StorePermissions.PaymentsReviewProof)).Succeeded;
        return View(order);
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.OrdersUpdateStatus)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string orderNumber, OrderStatus status, string? adminNotes, CancellationToken ct)
    {
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (!ModelState.IsValid || order is null || !await _orders.UpdateStatusAsync(order.Id, status, adminNotes, ct))
        {
            TempData["Error"] = "Transición no permitida. No se puede cancelar un pedido enviado o entregado; si está pagado, confirma primero su reembolso real.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Estado del pedido actualizado.";
        return RedirectToAction(nameof(Details), new { orderNumber });
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.PaymentsUpdate)]
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

    [HttpPost]
    [Authorize(Policy = StorePermissions.OrdersSetShipping)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmShipping(string orderNumber, decimal shippingCost, CancellationToken ct)
    {
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!ModelState.IsValid || order is null || string.IsNullOrWhiteSpace(actorId) ||
            !await _orders.ConfirmShippingAsync(order.Id, shippingCost, actorId, GetAuditRole(), ct))
        {
            TempData["Error"] = "No se pudo confirmar el recargo. Verifica el monto y que el pedido aún espere cotización.";
            return RedirectToAction(nameof(Details), new { orderNumber });
        }

        TempData["Success"] = "Recargo confirmado; el total y el monto pendiente de pago fueron actualizados.";
        return RedirectToAction(nameof(Details), new { orderNumber });
    }

    [HttpGet]
    [Authorize(Policy = StorePermissions.PaymentsViewProof)]
    public async Task<IActionResult> Proof(int id, CancellationToken ct)
    {
        var proof = await _proofs.GetAsync(id, ct);
        var key = await _proofs.GetStorageKeyAsync(id, ct);
        if (proof is null || key is null) return NotFound();
        var stream = await _proofStorage.OpenReadAsync(key, ct);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        return proof.ContentType == "application/pdf"
            ? File(stream, "application/octet-stream", $"comprobante-{id}.pdf")
            : File(stream, "image/jpeg");
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.PaymentsReviewProof)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewProof(int id, bool approve, string? note, CancellationToken ct)
    {
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var proof = await _proofs.GetAsync(id, ct);
        if (proof is null || string.IsNullOrWhiteSpace(actorId) || !await _proofs.ReviewAsync(id, approve, note, actorId, GetAuditRole(), ct))
        {
            TempData["Error"] = "No se pudo revisar el comprobante. Puede haber sido revisado ya o el envío aún no está confirmado.";
            return RedirectToAction(nameof(Index));
        }
        TempData["Success"] = approve ? "Comprobante aprobado y pago registrado." : "Comprobante rechazado; se notificó al cliente.";
        return RedirectToAction(nameof(Details), new { orderNumber = proof.OrderNumber });
    }

    private string GetAuditRole() => User.IsInRole(StoreRoles.Developer) ? StoreRoles.Developer
        : User.IsInRole(StoreRoles.Owner) ? StoreRoles.Owner : StoreRoles.LegacyAdmin;
}

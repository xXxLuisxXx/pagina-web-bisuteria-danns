using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Web.Models;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _db;
    private readonly Bisuteria.Web.Services.SecurityAuditLogger _audit;

    public OrdersController(IOrderService orders, IPaymentProofService proofs, Bisuteria.Web.Services.IPaymentProofStorage proofStorage,
        IAuthorizationService authorization, AppDbContext db, Bisuteria.Web.Services.SecurityAuditLogger audit)
    {
        _orders = orders;
        _proofs = proofs;
        _proofStorage = proofStorage;
        _authorization = authorization;
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(OrderStatus? status = null, string? search = null, int page = 1, CancellationToken ct = default, string? attention = null)
    {
        var requiredPermission = attention switch
        {
            null or "" or "old" => StorePermissions.OrdersView,
            "proofs" => StorePermissions.PaymentsReviewProof,
            "shipping" => StorePermissions.OrdersSetShipping,
            "unpaid" => StorePermissions.PaymentsUpdate,
            _ => null
        };
        if (requiredPermission is null) return BadRequest("Filtro de atención no válido.");
        if (!(await _authorization.AuthorizeAsync(User, null, requiredPermission)).Succeeded) return Forbid();
        ViewData["Title"] = "Pedidos";
        var result = await _orders.GetPagedAsync(page, 15, status, search, ct, attention);
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Attention = attention;
        ViewBag.AttentionLabel = attention switch
        {
            "proofs" => "Comprobantes por revisar",
            "shipping" => "Envíos por confirmar",
            "unpaid" => "Pagos pendientes",
            "old" => "Pedidos pendientes hace más de 48 horas",
            _ => null
        };
        ViewBag.Statuses = Enum.GetValues<OrderStatus>();
        ViewBag.StatusCounts = await _db.Orders.AsNoTracking().GroupBy(order => order.Status)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), ct);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string orderNumber, CancellationToken ct)
    {
        ViewData["Title"] = "Detalle del pedido";
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (order is null)
            return NotFound();

        if (User.IsInRole(StoreRoles.Developer) || User.IsInRole(StoreRoles.Owner))
        {
            Response.Headers.CacheControl = "no-store";
            await _audit.CustomerDataAccessAsync(order.UserId, "CustomerData.OrderDetailsViewed", ct);
        }
        else
        {
            order.CustomerName = "Datos reservados";
            order.CustomerEmail = "Oculto";
            order.CustomerPhone = "Oculto";
            order.AddressLine1 = "Oculta";
            order.AddressLine2 = null;
            order.PostalCode = null;
            order.City = "Oculta";
            order.Province = "Oculta";
            order.Observations = null;
            order.GiftRecipientName = null;
            order.GiftMessage = null;
        }

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
        ViewBag.CanUpdatePayment = (await _authorization.AuthorizeAsync(User, null, StorePermissions.PaymentsUpdate)).Succeeded;
        ViewBag.CanUpdateStatus = (await _authorization.AuthorizeAsync(User, null, StorePermissions.OrdersUpdateStatus)).Succeeded;

        var timeline = new List<OrderTimelineEntry> { new(order.CreatedAt, "Pedido recibido") };
        if (ViewBag.CanUpdatePayment is true)
            timeline.AddRange(await _db.Payments.AsNoTracking().Where(payment => payment.OrderId == order.Id && payment.PaidAt != null)
                .Select(payment => new OrderTimelineEntry(payment.PaidAt!.Value, "Pago registrado")).ToListAsync(ct));
        if (ViewBag.CanViewProof is true)
        {
            var proofEvents = await _db.PaymentProofs.AsNoTracking().Where(proof => proof.OrderId == order.Id)
                .Select(proof => new { proof.CreatedAt, proof.ReviewedAtUtc, proof.Status }).ToListAsync(ct);
            foreach (var proof in proofEvents)
            {
                timeline.Add(new(proof.CreatedAt, "Comprobante recibido"));
                if (proof.ReviewedAtUtc.HasValue)
                    timeline.Add(new(proof.ReviewedAtUtc.Value, proof.Status == PaymentProofStatus.Approved ? "Comprobante aprobado" : "Comprobante rechazado"));
            }
        }
        var entityId = order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var recordedEvents = await _db.Set<AuditLog>().AsNoTracking().Where(entry => entry.EntityType == "Order" &&
            entry.EntityId == entityId && (entry.Action == "Order.ShippingConfirmed" || entry.Action == "Order.StatusChanged" || entry.Action == "Payment.StatusChanged"))
            .Select(entry => new { entry.CreatedAt, entry.Action }).ToListAsync(ct);
        timeline.AddRange(recordedEvents.Select(entry => new OrderTimelineEntry(entry.CreatedAt, entry.Action switch
        {
            "Order.ShippingConfirmed" => "Recargo de envío confirmado",
            "Payment.StatusChanged" => "Estado del pago registrado",
            _ => "Estado del pedido registrado"
        })));
        ViewBag.Timeline = timeline.OrderBy(entry => entry.AtUtc).ToArray();
        return View(order);
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.OrdersUpdateStatus)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string orderNumber, OrderStatus status, string? adminNotes, CancellationToken ct)
    {
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        var beforeStatus = order?.Status;
        if (!ModelState.IsValid || order is null || !await _orders.UpdateStatusAsync(order.Id, status, adminNotes, ct))
        {
            TempData["Error"] = "Transición no permitida. No se puede cancelar un pedido enviado o entregado; si está pagado, confirma primero su reembolso real.";
            return RedirectToAction(nameof(Index));
        }

        await _audit.RecordAsync("Order.StatusChanged", "Order", order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), "200",
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { Status = beforeStatus!.Value.ToString() }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { Status = status.ToString() }), cancellationToken: ct);

        TempData["Success"] = "Estado del pedido actualizado.";
        return RedirectToAction(nameof(Details), new { orderNumber });
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.PaymentsUpdate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePayment(string orderNumber, PaymentStatus status, CancellationToken ct)
    {
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        var beforeStatus = order?.PaymentStatus;
        if (!ModelState.IsValid || order is null || !await _orders.UpdatePaymentStatusAsync(order.Id, status, ct))
        {
            TempData["Error"] = "El estado solicitado no es válido o el pedido está cancelado.";
            return RedirectToAction(nameof(Index));
        }

        await _audit.RecordAsync("Payment.StatusChanged", "Order", order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), "200",
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { Status = beforeStatus!.Value.ToString() }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { Status = status.ToString() }), cancellationToken: ct);

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
        var orderId = await _db.PaymentProofs.AsNoTracking().Where(item => item.Id == id).Select(item => item.OrderId).SingleOrDefaultAsync(ct);
        var userId = await _db.Orders.AsNoTracking().Where(item => item.Id == orderId).Select(item => item.UserId).SingleOrDefaultAsync(ct);
        await _audit.CustomerDataAccessAsync(userId, "CustomerData.ProofAccessed", ct);
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

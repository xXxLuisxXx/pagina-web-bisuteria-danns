using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Bisuteria.Web.Cart;
using Bisuteria.Web.Models;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bisuteria.Web.Controllers;

public class CheckoutController : Controller
{
    private const string TokenKey = "Bisuteria.CheckoutToken";
    private const string GuestAccessTokenKey = "Bisuteria.GuestOrderAccessToken";
    private readonly CartManager _cart;
    private readonly IOrderService _orders;
    private readonly ISettingService _settings;
    private readonly IConfiguration _configuration;
    private readonly IAuthorizationService _authorization;
    private readonly IPaymentProofService _proofs;
    private readonly IPaymentProofStorage _proofStorage;
    private PaymentMethod[] ConfiguredPayments => (_configuration.GetSection("Payments:EnabledMethods").Get<string[]>() ?? [])
        .Select(x => Enum.TryParse<PaymentMethod>(x, out var method) && method is PaymentMethod.CashOnDelivery or PaymentMethod.BankTransfer ? (PaymentMethod?)method : null)
        .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();

    public CheckoutController(CartManager cart, IOrderService orders, ISettingService settings, IConfiguration configuration,
        IAuthorizationService authorization, IPaymentProofService proofs, IPaymentProofStorage proofStorage)
    {
        _cart = cart;
        _orders = orders;
        _settings = settings;
        _configuration = configuration;
        _authorization = authorization;
        _proofs = proofs;
        _proofStorage = proofStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Finalizar compra";
        var availablePayments = await GetAvailablePaymentsAsync(ct);
        ViewBag.AvailablePayments = availablePayments;
        var cart = await _cart.GetCartAsync(ct);
        if (cart.IsEmpty)
            return RedirectToAction(nameof(CartController.Index), "Cart");

        var model = new CheckoutViewModel
        {
            CheckoutToken = Guid.TryParse(HttpContext.Session.GetString(TokenKey), out var saved) ? saved : Guid.NewGuid(),
            Cart = cart,
            CouponCode = cart.CouponCode,
            PaymentMethod = availablePayments.FirstOrDefault()
        };
        model.ShippingToConfirm = await NeedsShippingConfirmationAsync(model.DeliveryMethod, cart.Subtotal, ct);
        HttpContext.Session.SetString(TokenKey, model.CheckoutToken.Value.ToString());
        if (User.Identity?.IsAuthenticated != true && string.IsNullOrWhiteSpace(HttpContext.Session.GetString(GuestAccessTokenKey)))
            HttpContext.Session.SetString(GuestAccessTokenKey, Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));

        // Pre-rellenar datos del cliente autenticado.
        if (User.Identity?.IsAuthenticated == true)
        {
            var users = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Bisuteria.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.GetUserAsync(User);
            model.CustomerName = user is null ? "" : $"{user.FirstName} {user.LastName}".Trim();
            model.CustomerEmail = User.Identity.Name ?? string.Empty;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Finalizar compra";
        var availablePayments = await GetAvailablePaymentsAsync(ct);
        ViewBag.AvailablePayments = availablePayments;
        if (!model.CheckoutToken.HasValue || !Guid.TryParse(HttpContext.Session.GetString(TokenKey), out var expected) || expected != model.CheckoutToken)
            return BadRequest("La sesión de compra expiró. Recarga el formulario.");
        var cart = await _cart.GetCartAsync(ct);
        model.Cart = cart;
        model.ShippingToConfirm = await NeedsShippingConfirmationAsync(model.DeliveryMethod, cart.Subtotal, ct);

        if (cart.IsEmpty)
            return RedirectToAction(nameof(CartController.Index), "Cart");

        var validation = new Bisuteria.Application.Validators.CheckoutValidator().Validate(model.ToDto());
        foreach (var error in validation.Errors) ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        if (!availablePayments.Contains(model.PaymentMethod))
            ModelState.AddModelError(string.Empty, "No hay un método de pago habilitado para esta solicitud. Contacta con la tienda.");
        if (!ModelState.IsValid)
            return View(model);

        model.CouponCode = cart.CouponCode;
        var dto = model.ToDto();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        dto.GuestAccessToken = string.IsNullOrWhiteSpace(userId) ? HttpContext.Session.GetString(GuestAccessTokenKey) : null;
        OrderDto order;
        try
        {
            order = await _orders.CreateAsync(dto, cart, userId, ct);
        }
        catch (Bisuteria.Application.Services.BusinessRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        await _cart.ClearAsync();
        HttpContext.Session.Remove(TokenKey);

        TempData["OrderNumber"] = order.OrderNumber;
        HttpContext.Session.SetString("Bisuteria.LastOrderNumber", order.OrderNumber);
        if (order.GuestAccessToken is not null) HttpContext.Session.Remove(GuestAccessTokenKey);
        return RedirectToAction(nameof(Confirmation), new { orderNumber = order.OrderNumber, accessToken = order.GuestAccessToken });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(string orderNumber, string? accessToken, CancellationToken ct)
    {
        ViewData["Title"] = "Pedido confirmado";
        var order = await _orders.GetByNumberAsync(orderNumber, ct);
        if (order is null)
            return NotFound();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrWhiteSpace(userId) && order.UserId == userId;
        var isCurrentGuestOrder = string.Equals(HttpContext.Session.GetString("Bisuteria.LastOrderNumber"), orderNumber, StringComparison.Ordinal);
        var hasGuestToken = !string.IsNullOrWhiteSpace(accessToken) && await _orders.CanViewGuestOrderAsync(orderNumber, accessToken, ct);
        var canViewAll = (await _authorization.AuthorizeAsync(User, null, StorePermissions.OrdersView)).Succeeded;
        if (!canViewAll && !isOwner && !isCurrentGuestOrder && !hasGuestToken)
            return Challenge();

        ViewBag.AccessToken = accessToken;
        var paymentProofs = await _proofs.GetForOrderAsync(orderNumber, ct);
        ViewBag.PaymentProofs = paymentProofs;
        ViewBag.CanUploadPaymentProof = order.PaymentMethod == PaymentMethod.BankTransfer && !order.ShippingToConfirm &&
            order.PaymentStatus == PaymentStatus.Pending && order.Status != OrderStatus.Cancelled && paymentProofs.Count < 3;
        ViewBag.BankDetails = new Dictionary<string, string>
        {
            ["BankName"] = await _settings.GetAsync("BankName", "", ct) ?? "",
            ["BankAccountType"] = await _settings.GetAsync("BankAccountType", "", ct) ?? "",
            ["BankAccountNumber"] = await _settings.GetAsync("BankAccountNumber", "", ct) ?? "",
            ["BankAccountHolder"] = await _settings.GetAsync("BankAccountHolder", "", ct) ?? "",
            ["BankAccountId"] = await _settings.GetAsync("BankAccountId", "", ct) ?? ""
        };

        var whatsApp = await _settings.GetAsync("WhatsApp", "", ct);
        var digits = new string((whatsApp ?? "").Where(char.IsDigit).ToArray());
        ViewBag.WhatsAppUrl = digits.Length is >= 8 and <= 15
            ? "https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(BuildWhatsAppMessage(order))
            : null;

        return View(order);
    }

    [HttpGet]
    public IActionResult ThankYou() => RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-forms")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadPaymentProof(string orderNumber, string? accessToken, IFormFile? proof, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!await _proofs.CanAccessOrderAsync(orderNumber, userId, accessToken, ct)) return NotFound();
        if (proof is null)
        {
            TempData["Error"] = "Selecciona el comprobante que deseas enviar.";
            return RedirectToAction(nameof(Confirmation), new { orderNumber, accessToken });
        }

        StoredPaymentProof? stored = null;
        try
        {
            stored = await _proofStorage.SaveAsync(proof, ct);
            var proofId = await _proofs.AddAsync(orderNumber, userId, accessToken, stored.StorageKey,
                stored.ContentType, stored.Length, ct);
            if (!proofId.HasValue)
            {
                await _proofStorage.DeleteAsync(stored.StorageKey, ct);
                TempData["Error"] = "El comprobante no se pudo registrar. Verifica que sea una transferencia con envío confirmado y que no excedas tres archivos.";
            }
            else TempData["Success"] = "Comprobante recibido. La tienda revisará el archivo y te notificará el resultado.";
        }
        catch (Bisuteria.Application.Services.BusinessRuleException ex)
        {
            if (stored is not null) await _proofStorage.DeleteAsync(stored.StorageKey, ct);
            TempData["Error"] = ex.Message;
        }
        catch
        {
            if (stored is not null) await _proofStorage.DeleteAsync(stored.StorageKey, ct);
            throw;
        }
        return RedirectToAction(nameof(Confirmation), new { orderNumber, accessToken });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentProof(int id, string orderNumber, string? accessToken, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!await _proofs.CanAccessOrderAsync(orderNumber, userId, accessToken, ct)) return NotFound();
        var proof = await _proofs.GetAsync(id, ct);
        if (proof is null || proof.OrderNumber != orderNumber) return NotFound();
        var storageKey = await _proofs.GetStorageKeyAsync(id, ct);
        if (storageKey is null) return NotFound();
        var stream = await _proofStorage.OpenReadAsync(storageKey, ct);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        return proof.ContentType == "application/pdf"
            ? File(stream, "application/octet-stream", $"comprobante-{id}.pdf")
            : File(stream, "image/jpeg");
    }

    private async Task<bool> NeedsShippingConfirmationAsync(DeliveryMethod deliveryMethod, decimal subtotal, CancellationToken ct)
    {
        if (deliveryMethod == DeliveryMethod.Pickup) return false;
        var threshold = await _settings.GetFreeShippingThresholdAsync(ct);
        return deliveryMethod != DeliveryMethod.Shipping || !threshold.HasValue || subtotal < threshold.Value;
    }

    private async Task<PaymentMethod[]> GetAvailablePaymentsAsync(CancellationToken ct)
    {
        var methods = ConfiguredPayments.Where(method => method != PaymentMethod.BankTransfer).ToList();
        if (bool.TryParse(await _settings.GetAsync("PaymentBankTransferEnabled", "false", ct), out var enabled) && enabled)
            methods.Add(PaymentMethod.BankTransfer);
        return methods.Distinct().ToArray();
    }

    private static string BuildWhatsAppMessage(OrderDto order)
        => order.ShippingToConfirm
            ? $"Hola, quiero coordinar el pedido {order.OrderNumber}. El envío está pendiente de cotización."
            : $"Hola, quiero coordinar el pedido {order.OrderNumber} por un total de ${order.Total:0.00} USD.";
}

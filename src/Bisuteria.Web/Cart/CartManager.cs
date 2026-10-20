using System.Text.Json;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;

namespace Bisuteria.Web.Cart;

/// <summary>
/// Elemento persistido en sesión dentro del carrito.
/// </summary>
public sealed class CartItemState
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public int? CustomizationId { get; set; }

}

/// <summary>
/// Gestor del carrito de compras almacenado en la sesión del navegador.
/// </summary>
public sealed class CartManager
{
    private const string SessionKey = "cart_items";
    private const string CouponKey = "cart_coupon";

    private readonly IHttpContextAccessor _http;
    private readonly ICatalogService _catalog;
    private readonly ICouponService _coupons;
    private readonly ICustomizationService _customizations;

    public CartManager(
        IHttpContextAccessor http,
        ICatalogService catalog,
        ICouponService coupons,
        ICustomizationService customizations)
    {
        _http = http;
        _catalog = catalog;
        _coupons = coupons;
        _customizations = customizations;
    }

    public List<CartItemState> Items
    {
        get
        {
            var raw = _http.HttpContext?.Session.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(raw))
                return new List<CartItemState>();
            try
            {
                return JsonSerializer.Deserialize<List<CartItemState>>(raw) ?? new List<CartItemState>();
            }
            catch
            {
                return new List<CartItemState>();
            }
        }
        private set
        {
            _http.HttpContext?.Session.SetString(SessionKey, JsonSerializer.Serialize(value));
        }
    }

    /// <summary>Recupera el carrito completo con datos del producto, cupón y envío.</summary>
    public async Task<CartDto> GetCartAsync(CancellationToken ct = default)
    {
        var states = Items;
        var cart = new CartDto();

        if (states.Count > 0)
        {
            var ids = states.Select(s => s.ProductId).Distinct().ToList();
            var products = await _catalog.GetByIdsAsync(ids, ct);
            var map = products.ToDictionary(p => p.Id);

            foreach (var state in states)
            {
                if (!map.TryGetValue(state.ProductId, out var product))
                    continue;

                decimal price = product.EffectivePrice;

                cart.Items.Add(new CartItemDto
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    ImageUrl = product.MainImageUrl,
                    Slug = product.Slug,
                    UnitPrice = price,
                    Quantity = state.Quantity,
                    Stock = product.Stock,
                    CustomizationId = state.CustomizationId
                });
            }
        }

        // Personalizaciones (resumen).
        foreach (var item in cart.Items.Where(i => i.CustomizationId.HasValue))
        {
            var customization = await _customizations.GetAsync(item.CustomizationId!.Value, ct);
            if (customization is not null)
            {
                item.UnitPrice = Bisuteria.Application.Services.Pricing.ComputeCustomizationPrice(customization, item.UnitPrice);
                item.CustomizationSummary = BuildSummary(customization);
            }
        }

        // Cupón (se revalida siempre con el subtotal actual).
        var code = _http.HttpContext?.Session.GetString(CouponKey);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var validation = await _coupons.ValidateAsync(code, cart.Subtotal, ct);
            if (validation.IsValid)
            {
                cart.CouponCode = code;
                cart.Discount = validation.Discount;
            }
            else
            {
                cart.CouponMessage = validation.Message;
            }
        }

        // La dirección todavía no existe en el carrito. El envío se calcula al crear
        // el pedido, cuando el cliente ya indicó el destino y el método de entrega.
        cart.ShippingCost = 0m;

        return cart;
    }

    public Task AddAsync(int productId, int quantity, int? customizationId = null, CancellationToken ct = default)
    {
        var items = Items;
        var existing = items.FirstOrDefault(i => i.ProductId == productId && i.CustomizationId == customizationId);
        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            items.Add(new CartItemState
            {
                ProductId = productId,
                Quantity = quantity,
                CustomizationId = customizationId
            });
        }

        Items = items;
        return Task.CompletedTask;
    }

    public Task UpdateQuantityAsync(int productId, int quantity, int? customizationId = null)
    {
        var items = Items;
        var item = items.FirstOrDefault(i => i.ProductId == productId && i.CustomizationId == customizationId);
        if (item is not null)
        {
            if (quantity <= 0)
                items.Remove(item);
            else
                item.Quantity = quantity;
        }

        Items = items;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(int productId, int? customizationId = null)
    {
        var items = Items;
        items.RemoveAll(i => i.ProductId == productId && i.CustomizationId == customizationId);
        Items = items;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Items = new List<CartItemState>();
        _http.HttpContext?.Session.Remove(CouponKey);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync() => Task.FromResult(Items.Sum(i => i.Quantity));

    public Task<CouponValidationResult> ValidateCouponAsync(string code, decimal subtotal, CancellationToken ct = default)
        => _coupons.ValidateAsync(code, subtotal, ct);

    public Task ApplyCouponAsync(string code)
    {
        _http.HttpContext?.Session.SetString(CouponKey, code.Trim());
        return Task.CompletedTask;
    }

    public Task RemoveCouponAsync()
    {
        _http.HttpContext?.Session.Remove(CouponKey);
        return Task.CompletedTask;
    }

    private static string? BuildSummary(CustomizationInputDto c)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Text)) parts.Add($"Texto: {c.Text}");
        if (!string.IsNullOrWhiteSpace(c.Initials)) parts.Add($"Iniciales: {c.Initials}");
        if (!string.IsNullOrWhiteSpace(c.Date)) parts.Add($"Fecha: {c.Date}");
        if (!string.IsNullOrWhiteSpace(c.Phrase)) parts.Add($"Frase: {c.Phrase}");
        if (!string.IsNullOrWhiteSpace(c.Color)) parts.Add($"Color: {c.Color}");
        if (!string.IsNullOrWhiteSpace(c.Font)) parts.Add($"Fuente: {c.Font}");
        if (!string.IsNullOrWhiteSpace(c.Charm)) parts.Add($"Dije: {c.Charm}");
        if (!string.IsNullOrWhiteSpace(c.Material)) parts.Add($"Material: {c.Material}");
        if (!string.IsNullOrWhiteSpace(c.Size)) parts.Add($"Talla: {c.Size}");
        if (c.GiftWrap) parts.Add("Regalo");
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}

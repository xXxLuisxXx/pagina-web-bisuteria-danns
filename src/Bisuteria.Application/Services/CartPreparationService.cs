using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Validators;

namespace Bisuteria.Application.Services;

public sealed class CartPreparationService(ICatalogService catalog, ICustomizationService customizations)
{
    public async Task<int?> PrepareAsync(int productId, int quantity, int existingQuantity, CustomizationInputDto? customization, string quotaKey, CancellationToken ct = default)
    {
        var product = await catalog.GetProductAsync(productId, ct);
        if (product is null || !product.IsActive) throw new BusinessRuleException("El producto no está disponible.");
        if (quantity < 1 || existingQuantity < 0 || (long)quantity + existingQuantity > Math.Min(50, product.Stock))
            throw new BusinessRuleException("La cantidad supera las existencias disponibles o el límite de 50 unidades.");
        var input = customization ?? new CustomizationInputDto();
        // El empaque de regalo ya no se ofrece en el personalizador; ignora valores enviados manualmente.
        input.GiftWrap = false;
        InputValidation.Check(new CustomizationInputValidator(), input);
        var hasCustomization = new[] { input.Text, input.Initials, input.Date, input.Phrase, input.Color, input.Font, input.Charm, input.Material, input.Size, input.Note }.Any(x => !string.IsNullOrWhiteSpace(x));
        if (!hasCustomization) return null;
        if (!product.IsCustomizable) throw new BusinessRuleException("Este producto no admite personalización.");
        input.ProductId = productId;
        input.Quantity = quantity;
        input.PriceOverride = Pricing.ComputeCustomizationPrice(input, product.EffectivePrice);
        return await customizations.SaveAsync(input, ct, quotaKey);
    }
}

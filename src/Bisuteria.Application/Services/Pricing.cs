using Bisuteria.Application.DTOs.Cart;

namespace Bisuteria.Application.Services;

/// <summary>
/// Cálculo de precios de la personalización (lado servidor para evitar manipulación).
/// </summary>
public static class Pricing
{
    public const decimal TextPrice = 4.00m;
    public const decimal InitialsPrice = 3.00m;
    public const decimal DatePrice = 3.00m;
    public const decimal PhrasePrice = 5.00m;
    public const decimal CharmPrice = 5.00m;
    public const decimal GiftWrapPrice = 3.00m;

    /// <summary>Calcula el precio final de un producto con su personalización.</summary>
    public static decimal ComputeCustomizationPrice(CustomizationInputDto dto, decimal basePrice)
    {
        var extra = 0m;

        if (!string.IsNullOrWhiteSpace(dto.Text)) extra += TextPrice;
        if (!string.IsNullOrWhiteSpace(dto.Initials)) extra += InitialsPrice;
        if (!string.IsNullOrWhiteSpace(dto.Date)) extra += DatePrice;
        if (!string.IsNullOrWhiteSpace(dto.Phrase)) extra += PhrasePrice;
        if (!string.IsNullOrWhiteSpace(dto.Charm)) extra += CharmPrice;
        if (dto.GiftWrap) extra += GiftWrapPrice;

        return basePrice + extra;
    }
}

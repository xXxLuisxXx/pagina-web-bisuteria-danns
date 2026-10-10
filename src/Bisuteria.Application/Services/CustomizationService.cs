using Bisuteria.Application.Validators;
using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de personalizaciones.
/// </summary>
public class CustomizationService : ICustomizationService
{
    private readonly IUnitOfWork _unitOfWork;

    public CustomizationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<int> SaveAsync(CustomizationInputDto dto, CancellationToken ct = default, string? quotaKey = null)
        => _unitOfWork.InTransactionAsync(() => SaveCoreAsync(dto, quotaKey, ct), ct);

    private async Task<int> SaveCoreAsync(CustomizationInputDto dto, string? quotaKey, CancellationToken ct)
    {
        InputValidation.Check(new CustomizationInputValidator(), dto);
        var hash = quotaKey is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(quotaKey)));
        if (hash is not null && await _unitOfWork.Repository<Customization>().CountAsync(c => c.QuotaKey == hash, ct) >= 100)
            throw new BusinessRuleException("Alcanzaste el límite de personalizaciones de esta sesión.");
        var entity = new Customization
        {
            QuotaKey = hash,
            ProductId = dto.ProductId,
            Text = dto.Text,
            Initials = dto.Initials,
            Date = dto.Date,
            Phrase = dto.Phrase,
            Color = dto.Color,
            Font = dto.Font,
            Charm = dto.Charm,
            Material = dto.Material,
            Size = dto.Size,
            Quantity = dto.Quantity,
            GiftWrap = dto.GiftWrap,
            Note = dto.Note,
            PriceOverride = dto.PriceOverride
        };

        var repo = _unitOfWork.Repository<Customization>();
        await repo.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<CustomizationInputDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.Repository<Customization>().GetByIdAsync(id, ct);
        return entity is null
            ? null
            : EntityMappings.ToInput(entity);
    }
}

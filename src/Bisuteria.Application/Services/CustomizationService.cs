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

    public async Task<int> SaveAsync(CustomizationInputDto dto, CancellationToken ct = default)
    {
        InputValidation.Check(new CustomizationInputValidator(), dto);
        var entity = new Customization
        {
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

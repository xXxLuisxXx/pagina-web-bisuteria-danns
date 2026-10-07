using Bisuteria.Application.DTOs.Cart;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio que persiste las personalizaciones creadas en el Personalizador.
/// </summary>
public interface ICustomizationService
{
    /// <summary>Guarda una personalización y devuelve su Id.</summary>
    Task<int> SaveAsync(CustomizationInputDto dto, CancellationToken cancellationToken = default);

    /// <summary>Recupera una personalización por su Id.</summary>
    Task<CustomizationInputDto?> GetAsync(int id, CancellationToken cancellationToken = default);
}

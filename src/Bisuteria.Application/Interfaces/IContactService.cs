using Bisuteria.Application.DTOs.Admin;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio de mensajes de contacto.
/// </summary>
public interface IContactService
{
    Task SendMessageAsync(string name, string email, string? phone, string subject, string message, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(int id, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<List<ContactMessageDto>> GetMessagesAsync(bool unreadOnly = false, CancellationToken cancellationToken = default);
}

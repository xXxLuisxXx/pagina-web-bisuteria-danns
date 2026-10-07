using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de contacto.
/// </summary>
public class ContactService : IContactService
{
    private readonly IUnitOfWork _unitOfWork;

    public ContactService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task SendMessageAsync(string name, string email, string? phone, string subject, string message, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<ContactMessage>();
        await repo.AddAsync(new ContactMessage
        {
            Name = name,
            Email = email,
            Phone = phone,
            Subject = subject,
            Message = message
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<int> CountUnreadAsync(CancellationToken ct = default)
        => await _unitOfWork.Repository<ContactMessage>().CountAsync(m => !m.IsRead, ct);

    public async Task MarkAsReadAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<ContactMessage>();
        var msg = await repo.GetByIdAsync(id, ct);
        if (msg is null)
            return;
        msg.IsRead = true;
        repo.Update(msg);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<ContactMessage>();
        var msg = await repo.GetByIdAsync(id, ct);
        if (msg is null)
            return;
        repo.Remove(msg);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<ContactMessageDto>> GetMessagesAsync(bool unreadOnly = false, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<ContactMessage>();
        return (await repo.ListAsync(m => !unreadOnly || !m.IsRead, ct))
            .OrderByDescending(m => m.Id)
            .Select(m => new ContactMessageDto
            {
                Id = m.Id,
                Name = m.Name,
                Email = m.Email,
                Phone = m.Phone,
                Subject = m.Subject,
                Message = m.Message,
                IsRead = m.IsRead,
                CreatedAt = m.CreatedAt
            })
            .ToList();
    }
}

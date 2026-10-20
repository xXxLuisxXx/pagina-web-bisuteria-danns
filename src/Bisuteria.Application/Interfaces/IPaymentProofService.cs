using Bisuteria.Application.DTOs.Orders;

namespace Bisuteria.Application.Interfaces;

public interface IPaymentProofService
{
    Task<bool> CanAccessOrderAsync(string orderNumber, string? userId, string? accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentProofDto>> GetForOrderAsync(string orderNumber, CancellationToken ct = default);
    Task<PaymentProofDto?> GetAsync(int proofId, CancellationToken ct = default);
    Task<string?> GetStorageKeyAsync(int proofId, CancellationToken ct = default);
    Task<int?> AddAsync(string orderNumber, string? userId, string? accessToken, string storageKey,
        string contentType, long length, CancellationToken ct = default);
    Task<bool> ReviewAsync(int proofId, bool approve, string? note, string actorId, string actorRole, CancellationToken ct = default);
}

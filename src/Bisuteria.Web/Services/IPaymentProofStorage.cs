namespace Bisuteria.Web.Services;

public sealed record StoredPaymentProof(string StorageKey, string ContentType, long Length);

public interface IPaymentProofStorage
{
    Task<StoredPaymentProof> SaveAsync(IFormFile file, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

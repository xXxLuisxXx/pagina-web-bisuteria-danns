namespace Bisuteria.Domain.Entities;

/// <summary>
/// Esquema compatible con Microsoft.Extensions.Caching.SqlServer para guardar sesiones compartidas.
/// La aplicación accede a estos registros únicamente mediante IDistributedCache.
/// </summary>
public sealed class DistributedCacheRecord
{
    public string Id { get; set; } = string.Empty;
    public byte[] Value { get; set; } = [];
    public DateTimeOffset ExpiresAtTime { get; set; }
    public long? SlidingExpirationInSeconds { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}

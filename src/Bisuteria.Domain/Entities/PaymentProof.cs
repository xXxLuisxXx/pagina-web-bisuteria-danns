using Bisuteria.Domain.Common;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Domain.Entities;

/// <summary>Metadatos de un comprobante privado de transferencia asociado al pago.</summary>
public class PaymentProof : BaseEntity
{
    public int OrderId { get; set; }
    public virtual Order? Order { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public PaymentProofStatus Status { get; set; } = PaymentProofStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}

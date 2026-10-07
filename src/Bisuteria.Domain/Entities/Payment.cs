using Bisuteria.Domain.Common;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Registro de pago asociado a un pedido.
/// </summary>
public class Payment : BaseEntity
{
    public int OrderId { get; set; }

    public virtual Order? Order { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    /// <summary>Referencia de la transferencia o transacción.</summary>
    public string? Reference { get; set; }

    /// <summary>Identificador de la transacción en la pasarela (futuro).</summary>
    public string? TransactionId { get; set; }

    public DateTime? PaidAt { get; set; }
}

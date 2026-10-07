namespace Bisuteria.Domain.Enums;

/// <summary>
/// Estados de un pago.
/// </summary>
public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

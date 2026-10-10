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

public static class PaymentStatusExtensions
{
    public static string GetLabel(this PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Pendiente",
        PaymentStatus.Paid => "Pagado",
        PaymentStatus.Failed => "Fallido",
        PaymentStatus.Refunded => "Reembolsado",
        _ => "Desconocido"
    };
}

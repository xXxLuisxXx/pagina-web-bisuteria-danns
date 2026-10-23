namespace Bisuteria.Domain.Enums;

/// <summary>
/// Métodos de pago soportados por el sistema.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Abono inicial y saldo en efectivo coordinados con la tienda.</summary>
    CashOnDelivery = 1,

    /// <summary>Transferencia bancaria.</summary>
    BankTransfer = 2,

    /// <summary>Tarjeta de crédito o débito.</summary>
    Card = 3
}

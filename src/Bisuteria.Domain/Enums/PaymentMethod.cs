namespace Bisuteria.Domain.Enums;

/// <summary>
/// Métodos de pago soportados por el sistema.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Pago contra entrega.</summary>
    CashOnDelivery = 1,

    /// <summary>Transferencia bancaria.</summary>
    BankTransfer = 2,

    /// <summary>Tarjeta de crédito o débito.</summary>
    Card = 3
}

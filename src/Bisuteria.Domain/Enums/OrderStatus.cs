namespace Bisuteria.Domain.Enums;

/// <summary>
/// Estados del ciclo de vida de un pedido.
/// </summary>
public enum OrderStatus
{
    /// <summary>Pedido recibido, a la espera de confirmación.</summary>
    Pending = 1,

    /// <summary>El pedido fue confirmado con el cliente.</summary>
    Confirmed = 2,

    /// <summary>El pedido se encuentra en preparación.</summary>
    Preparing = 3,

    /// <summary>El pedido está listo para retiro o envío.</summary>
    Ready = 4,

    /// <summary>El pedido fue enviado al cliente.</summary>
    Shipped = 5,

    /// <summary>El pedido fue entregado al cliente.</summary>
    Delivered = 6,

    /// <summary>El pedido fue cancelado.</summary>
    Cancelled = 7
}

/// <summary>
/// Etiquetas y estilos en español para los estados de pedido.
/// </summary>
public static class OrderStatusExtensions
{
    public static string GetLabel(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pendiente",
        OrderStatus.Confirmed => "Confirmado",
        OrderStatus.Preparing => "En preparación",
        OrderStatus.Ready => "Listo",
        OrderStatus.Shipped => "Enviado",
        OrderStatus.Delivered => "Entregado",
        OrderStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    public static string GetBadgeClass(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bg-warning text-dark",
        OrderStatus.Confirmed => "bg-info text-dark",
        OrderStatus.Preparing => "bg-primary",
        OrderStatus.Ready => "bg-secondary",
        OrderStatus.Shipped => "bg-dark",
        OrderStatus.Delivered => "bg-success",
        OrderStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };
}

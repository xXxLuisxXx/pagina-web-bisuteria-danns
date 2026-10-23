namespace Bisuteria.Domain.Enums;

/// <summary>Reglas compartidas por el servicio y el formulario administrativo.</summary>
public static class OrderTransitions
{
    public static bool Allows(OrderStatus current, OrderStatus next, PaymentStatus payment)
    {
        if (!Enum.IsDefined(current) || !Enum.IsDefined(next) || !Enum.IsDefined(payment)) return false;
        if (current == next) return true; // Permite actualizar notas sin mover inventario.
        if (current == OrderStatus.Pending && next == OrderStatus.Confirmed && payment is not (PaymentStatus.PartiallyPaid or PaymentStatus.Paid)) return false;
        if (next is OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Shipped && payment is not (PaymentStatus.PartiallyPaid or PaymentStatus.Paid)) return false;
        if (next == OrderStatus.Cancelled)
            return current is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready
                && payment is not (PaymentStatus.Paid or PaymentStatus.PartiallyPaid);
        return (current, next) is
            (OrderStatus.Pending, OrderStatus.Confirmed) or
            (OrderStatus.Confirmed, OrderStatus.Preparing) or
            (OrderStatus.Preparing, OrderStatus.Ready) or
            (OrderStatus.Ready, OrderStatus.Shipped) or
            (OrderStatus.Shipped, OrderStatus.Delivered);
    }

    public static bool AllowsPayment(OrderStatus order, PaymentStatus current, PaymentStatus next)
        => Enum.IsDefined(order) && order != OrderStatus.Cancelled && Enum.IsDefined(current) && Enum.IsDefined(next)
        && (current == next || (current, next) is
            (PaymentStatus.Pending, PaymentStatus.Paid) or
            (PaymentStatus.Pending, PaymentStatus.PartiallyPaid) or
            (PaymentStatus.Pending, PaymentStatus.Failed) or
            (PaymentStatus.Failed, PaymentStatus.Pending) or
            (PaymentStatus.Failed, PaymentStatus.PartiallyPaid) or
            (PaymentStatus.Failed, PaymentStatus.Paid) or
            (PaymentStatus.PartiallyPaid, PaymentStatus.Paid) or
            (PaymentStatus.PartiallyPaid, PaymentStatus.Refunded) or
            (PaymentStatus.Paid, PaymentStatus.Refunded));
}

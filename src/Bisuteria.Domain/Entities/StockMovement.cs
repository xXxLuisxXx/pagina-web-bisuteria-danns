using Bisuteria.Domain.Common;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Movimiento de inventario (entrada o salida) sobre un producto.
/// Permite auditar cambios de stock y generar alertas.
/// </summary>
public class StockMovement : BaseEntity
{
    public int ProductId { get; set; }

    public virtual Product? Product { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Cantidad movida (siempre positiva; el signo depende del tipo).</summary>
    public int Quantity { get; set; }

    /// <summary>Stock resultante después del movimiento.</summary>
    public int StockAfter { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }
}

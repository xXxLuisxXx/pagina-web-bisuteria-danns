using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>Sector atendido por la tienda y tarifa vigente para ese destino.</summary>
public class DeliveryZone : BaseEntity
{
    public string Province { get; set; } = "Manabí";
    public string City { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public bool IsFree { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string? EstimatedTime { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

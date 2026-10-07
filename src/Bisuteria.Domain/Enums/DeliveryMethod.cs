namespace Bisuteria.Domain.Enums;

/// <summary>
/// Métodos de entrega disponibles para un pedido.
/// </summary>
public enum DeliveryMethod
{
    /// <summary>Retiro en punto acordado con la tienda.</summary>
    Pickup = 1,

    /// <summary>Entrega local dentro de la ciudad.</summary>
    LocalDelivery = 2,

    /// <summary>Envío a domicilio mediante courier.</summary>
    Shipping = 3
}

/// <summary>
/// Etiquetas en español para los métodos de entrega.
/// </summary>
public static class DeliveryMethodExtensions
{
    public static string GetLabel(this DeliveryMethod method) => method switch
    {
        DeliveryMethod.Pickup => "Retiro en tienda",
        DeliveryMethod.LocalDelivery => "Entrega local",
        DeliveryMethod.Shipping => "Envío a domicilio",
        _ => method.ToString()
    };
}

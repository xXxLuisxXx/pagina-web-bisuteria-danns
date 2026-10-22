using Bisuteria.Application.DTOs.Orders;

namespace Bisuteria.Application.Interfaces;

/// <summary>Construye un canal de contacto de pedido que el cliente confirma manualmente.</summary>
public interface IOrderNotificationChannel
{
    string? CreateWhatsAppUrl(OrderDto order, string? configuredPhone);
}

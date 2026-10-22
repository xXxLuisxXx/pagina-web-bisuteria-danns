using System.Globalization;
using System.Text;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Web.Services;

/// <summary>Genera un mensaje acotado; el cliente lo revisa y lo envía desde WhatsApp.</summary>
public sealed class WhatsAppOrderNotificationChannel : IOrderNotificationChannel
{
    private const int MessageLimit = 1800;

    public string? CreateWhatsAppUrl(OrderDto order, string? configuredPhone)
    {
        var digits = new string((configuredPhone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 15) return null;

        var builder = new StringBuilder()
            .AppendLine("Solicitud de revisión de compra")
            .Append("Pedido: ").Append(order.OrderNumber).AppendLine()
            .AppendLine("Productos:");
        foreach (var item in order.Items)
        {
            var line = $"• {item.ProductName} × {item.Quantity}" +
                (string.IsNullOrWhiteSpace(item.CustomizationSummary) ? "" : $" — {item.CustomizationSummary}");
            AppendWithinLimit(builder, line);
        }

        builder.AppendLine().Append("Cliente: ").Append(Clean(order.CustomerName, 100)).AppendLine()
            .Append("Teléfono: ").Append(Clean(order.CustomerPhone, 40)).AppendLine()
            .Append("Entrega: ").Append(order.DeliveryMethodLabel).AppendLine();
        if (order.DeliveryMethod != DeliveryMethod.Pickup)
            builder.Append("Destino: ").Append(Clean(string.Join(", ", new[] { order.DeliverySector, order.City }
                .Where(value => !string.IsNullOrWhiteSpace(value))), 140)).AppendLine();
        if (order.ShippingToConfirm)
            builder.AppendLine("Envío y total: pendientes de cotización.");
        else
            builder.Append("Total: $").Append(order.Total.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" USD");

        var message = builder.ToString().Trim();
        if (message.Length > MessageLimit) message = message[..(MessageLimit - 1)].TrimEnd() + "…";
        return "https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(message);
    }

    private static void AppendWithinLimit(StringBuilder builder, string value)
    {
        var remaining = MessageLimit - builder.Length - 260;
        if (remaining <= 1) return;
        builder.AppendLine(value.Length > remaining ? value[..(remaining - 1)].TrimEnd() + "…" : value);
    }

    private static string Clean(string? value, int maxLength)
    {
        var filtered = new string((value ?? "").Where(character => !char.IsControl(character)).ToArray());
        var cleaned = string.Join(' ', filtered.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length > maxLength ? cleaned[..(maxLength - 1)].TrimEnd() + "…" : cleaned;
    }
}

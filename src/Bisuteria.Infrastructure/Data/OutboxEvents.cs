using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Bisuteria.Infrastructure.Data;

public partial class AppDbContext
{
    // La notificación y el cambio de negocio se persisten en la misma operación SQL.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var orders = ChangeTracker.Entries<Order>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        var products = ChangeTracker.Entries<Product>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        var proofs = ChangeTracker.Entries<PaymentProof>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        var contact = ChangeTracker.Entries<ContactMessage>().Any(e => e.State == EntityState.Added);
        string? storeEmail = null;
        if (orders.Length > 0 || products.Length > 0 || proofs.Length > 0 || contact)
            storeEmail = await SiteSettings.AsNoTracking().Where(s => s.Key == "ContactEmail").Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        void Queue(string? recipient, string subject, string text)
        {
            if (string.IsNullOrWhiteSpace(recipient) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(recipient)) return;
            if (Set<EmailOutbox>().Local.Any(x => Entry(x).State == EntityState.Added && x.Recipient == recipient && x.Subject == subject && x.TextBody == text)) return;
            Add(new EmailOutbox { Recipient = recipient, Subject = subject, TextBody = text });
        }
        foreach (var entry in ChangeTracker.Entries<Bisuteria.Infrastructure.Identity.ApplicationUser>().ToArray())
        {
            if (entry.State == EntityState.Added)
                Queue(entry.Entity.Email, "Bienvenido a Bisuterías Dann’s", "Tu cuenta fue creada. Desde tu perfil puedes consultar pedidos y activar la verificación en dos pasos.");
            else if (entry.State == EntityState.Modified && entry.Property(u => u.PasswordHash).OriginalValue != entry.Entity.PasswordHash)
                Queue(entry.Entity.Email, "Tu contraseña fue cambiada", "La contraseña de tu cuenta fue actualizada. Si no reconoces esta acción, recupera tu acceso y contacta con la tienda.");
        }
        foreach (var entry in orders)
        {
            var order = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                var items = string.Join("\n", order.Items.Select(i => $"{i.ProductName} × {i.Quantity}"));
                var delivery = order.ShippingToConfirm ? "Envío: por confirmar según el destino." : $"Envío: {order.ShippingCost.ToString("F2", CultureInfo.InvariantCulture)} USD.";
                var guestAccess = order.GuestAccessToken is null ? "" : $"\nCódigo privado para consultar tu pedido: {order.GuestAccessToken}\nAbre /Checkout/Confirmation?orderNumber={order.OrderNumber}&accessToken={Uri.EscapeDataString(order.GuestAccessToken)} en el sitio de la tienda.";
                var text = $"Pedido {order.OrderNumber}\n{items}\nSubtotal: {order.Subtotal.ToString("F2", CultureInfo.InvariantCulture)} USD\nDescuento: {order.Discount.ToString("F2", CultureInfo.InvariantCulture)} USD\n{delivery}\n{(order.ShippingToConfirm ? "Total parcial, pendiente de confirmar envío" : $"Total: {order.Total.ToString("F2", CultureInfo.InvariantCulture)} USD")}\nMétodo: {(order.PaymentMethod == PaymentMethod.BankTransfer ? "Transferencia bancaria" : "Pago contra entrega")}. El pago está pendiente de verificación.{guestAccess}";
                Queue(order.CustomerEmail, "Confirmación de pedido", text);
                Queue(storeEmail, "Pedido nuevo", $"Se recibió el pedido {order.OrderNumber}. Revisa sus detalles en el panel administrativo.");
            }
            else
            {
                if (entry.Property(o => o.Status).OriginalValue != order.Status)
                {
                    var status = order.Status switch { OrderStatus.Confirmed => "confirmado", OrderStatus.Preparing => "en preparación", OrderStatus.Ready => "listo", OrderStatus.Shipped => "enviado", OrderStatus.Delivered => "entregado", OrderStatus.Cancelled => "cancelado", _ => "pendiente" };
                    Queue(order.CustomerEmail, "Estado de tu pedido", $"El pedido {order.OrderNumber} está {status}." + (order.Status == OrderStatus.Shipped && !string.IsNullOrWhiteSpace(order.TrackingNumber) ? $" Guía: {order.TrackingNumber}." : ""));
                }
                if (entry.Property(o => o.PaymentStatus).OriginalValue != order.PaymentStatus && order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded)
                    Queue(order.CustomerEmail, "Estado de tu pago", $"El pago del pedido {order.OrderNumber} fue registrado como {(order.PaymentStatus == PaymentStatus.Paid ? "recibido" : "reembolsado")} por la tienda.");
                if (entry.Property(o => o.ShippingCost).OriginalValue != order.ShippingCost ||
                    (entry.Property(o => o.ShippingToConfirm).OriginalValue && !order.ShippingToConfirm))
                    Queue(order.CustomerEmail, "Recargo de envío confirmado", $"El envío del pedido {order.OrderNumber} quedó en {order.ShippingCost.ToString("F2", CultureInfo.InvariantCulture)} USD. El total actualizado es {order.Total.ToString("F2", CultureInfo.InvariantCulture)} USD. Puedes continuar con el pago.");
            }
        }
        foreach (var entry in proofs)
        {
            if (entry.State == EntityState.Added)
                Queue(storeEmail, "Nuevo comprobante de pago", "Hay un comprobante nuevo pendiente de revisión en el panel de pedidos.");
            else if (entry.Property(x => x.Status).IsModified && entry.Entity.Order is not null)
            {
                var result = entry.Entity.Status == PaymentProofStatus.Approved ? "aprobado" : "rechazado";
                var note = string.IsNullOrWhiteSpace(entry.Entity.ReviewNote) ? "" : $" Motivo: {entry.Entity.ReviewNote}";
                Queue(entry.Entity.Order.CustomerEmail, "Revisión de comprobante", $"El comprobante del pedido {entry.Entity.Order.OrderNumber} fue {result}.{note}");
            }
        }
        if (contact) Queue(storeEmail, "Mensaje de contacto nuevo", "Hay un nuevo mensaje de contacto. Consúltalo en el panel administrativo.");
        foreach (var entry in products)
        {
            var product = entry.Entity;
            if (product.Stock > product.MinStock) product.LowStockNotified = false;
            else if (product.IsActive && !product.LowStockNotified && !string.IsNullOrWhiteSpace(storeEmail))
            {
                Queue(storeEmail, "Alerta de stock bajo", $"El producto {product.Name} tiene {product.Stock} unidades disponibles. Revisa el inventario.");
                product.LowStockNotified = true;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}

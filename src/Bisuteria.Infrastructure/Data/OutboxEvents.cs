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
        var settings = new Dictionary<string, string?>();
        if (orders.Length > 0 || products.Length > 0 || proofs.Length > 0 || contact)
            settings = await SiteSettings.AsNoTracking()
                .Where(s => s.Key == "ContactEmail" || s.Key == "BankName" || s.Key == "BankAccountType" ||
                    s.Key == "BankAccountNumber" || s.Key == "BankAccountHolder")
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        var storeEmail = settings.GetValueOrDefault("ContactEmail");
        var proofOrderIds = proofs.Select(p => p.Entity.OrderId).Distinct().ToArray();
        var proofOrders = new List<Order>();
        if (proofOrderIds.Length > 0)
            proofOrders = await Orders.AsNoTracking().Where(o => proofOrderIds.Contains(o.Id))
                .Select(o => new Order { Id = o.Id, OrderNumber = o.OrderNumber, CustomerEmail = o.CustomerEmail })
                .ToListAsync(cancellationToken);
        static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture) + " USD";
        string PublicUrl(string path) => _publicBaseUri is null ? path : new Uri(_publicBaseUri, path).AbsoluteUri;
        string BankInstructions(Order order)
        {
            if (order.PaymentMethod != PaymentMethod.BankTransfer) return "";
            var keys = new[] { "BankName", "BankAccountType", "BankAccountNumber", "BankAccountHolder" };
            if (keys.Any(key => string.IsNullOrWhiteSpace(settings.GetValueOrDefault(key))))
                return "\nConsulta los datos de transferencia en la confirmación del pedido o con la tienda.";
            return $"\nDatos para transferir:\nBanco: {settings["BankName"]}\nTipo de cuenta: {settings["BankAccountType"]}\nCuenta: {settings["BankAccountNumber"]}\nTitular: {settings["BankAccountHolder"]}" +
                (order.ShippingToConfirm ? "\nEspera a que la tienda confirme el envío y el total antes de pagar." : "\nIncluye el número de pedido como referencia y carga el comprobante en la confirmación del pedido.");
        }
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
                var items = string.Join("\n", order.Items.Select(i => $"{i.ProductName} × {i.Quantity} — {Money(i.UnitPrice)} por unidad; {Money(i.LineTotal)}" +
                    (string.IsNullOrWhiteSpace(i.CustomizationSummary) ? "" : $"\nPersonalización: {i.CustomizationSummary}")));
                var delivery = order.ShippingToConfirm ? "Envío: por confirmar según el destino." : $"Envío: {Money(order.ShippingCost)}.";
                var confirmationPath = $"/Checkout/Confirmation?orderNumber={Uri.EscapeDataString(order.OrderNumber)}";
                if (order.GuestAccessToken is not null)
                    confirmationPath += $"&accessToken={Uri.EscapeDataString(order.GuestAccessToken)}";
                var customerAccess = $"\nConsulta tu pedido: {PublicUrl(confirmationPath)}\nNo compartas este enlace privado.";
                var summary = $"Pedido {order.OrderNumber}\n{items}\nSubtotal: {Money(order.Subtotal)}\nDescuento: {Money(order.Discount)}\n{delivery}\n{(order.ShippingToConfirm ? $"Total parcial, pendiente de confirmar envío: {Money(order.Total)}" : $"Total: {Money(order.Total)}")}\nEntrega: {order.DeliveryMethod.GetLabel()}\nMétodo: {(order.PaymentMethod == PaymentMethod.BankTransfer ? "Transferencia bancaria" : "Pago contra entrega")}. El pago está pendiente de verificación.";
                var text = summary + BankInstructions(order) + customerAccess;
                Queue(order.CustomerEmail, "Confirmación de pedido", text);
                var address = order.DeliveryMethod == DeliveryMethod.Pickup ? "Retiro en tienda" :
                    string.Join(", ", new[] { order.AddressLine1, order.AddressLine2, order.City, order.Province, order.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x)));
                Queue(storeEmail, "Pedido nuevo", summary + $"\nCliente: {order.CustomerName}\nCorreo: {order.CustomerEmail}\nTeléfono: {order.CustomerPhone}\nDestino: {address}" +
                    (string.IsNullOrWhiteSpace(order.Observations) ? "" : $"\nObservaciones: {order.Observations}") +
                    (order.IsGift ? $"\nRegalo para: {order.GiftRecipientName}\nMensaje: {order.GiftMessage}" : "") +
                    $"\nRevisa el pedido en el panel: {PublicUrl($"/Admin/Orders/Details?orderNumber={Uri.EscapeDataString(order.OrderNumber)}")}");
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
                    Queue(order.CustomerEmail, "Recargo de envío confirmado", $"El envío del pedido {order.OrderNumber} quedó en {Money(order.ShippingCost)}. El total actualizado es {Money(order.Total)}. Puedes continuar con el pago." + BankInstructions(order));
            }
        }
        foreach (var entry in proofs)
        {
            var order = proofOrders.SingleOrDefault(o => o.Id == entry.Entity.OrderId);
            if (order is null) continue;
            if (entry.State == EntityState.Added)
            {
                Queue(storeEmail, "Nuevo comprobante de pago", $"El pedido {order.OrderNumber} tiene un comprobante nuevo pendiente de revisión en el panel de pedidos.");
                Queue(order.CustomerEmail, "Comprobante recibido", $"Recibimos el comprobante del pedido {order.OrderNumber}. La tienda lo revisará; recibirlo no significa que el pago esté aprobado.");
            }
            else if (entry.Property(x => x.Status).OriginalValue != entry.Entity.Status && entry.Entity.Status is PaymentProofStatus.Approved or PaymentProofStatus.Rejected)
            {
                var result = entry.Entity.Status == PaymentProofStatus.Approved ? "aprobado" : "rechazado";
                var note = string.IsNullOrWhiteSpace(entry.Entity.ReviewNote) ? "" : $" Motivo: {entry.Entity.ReviewNote}";
                Queue(order.CustomerEmail, "Revisión de comprobante", $"El comprobante del pedido {order.OrderNumber} fue {result}.{note}");
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

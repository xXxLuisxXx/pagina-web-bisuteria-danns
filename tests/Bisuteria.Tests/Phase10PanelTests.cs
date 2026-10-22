using System.Net;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task DashboardAlertsAndOrderFiltersIncludePendingProofsAndShipping()
    {
        using var scope = f.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var marker = "Panel-" + Guid.NewGuid().ToString("N");
        Order Sample(string suffix, bool shipping) => new()
        {
            OrderNumber = "P10-" + Guid.NewGuid().ToString("N")[..20], CustomerName = marker + suffix,
            CustomerEmail = "panel@example.test", CustomerPhone = "2025550100", ShippingToConfirm = shipping,
            DeliveryMethod = DeliveryMethod.Shipping, Status = OrderStatus.Pending
        };
        var awaitingProof = Sample("-proof", false);
        var awaitingShipping = Sample("-shipping", true);
        var ordinary = Sample("-ordinary", false);
        awaitingProof.PaymentProofs.Add(new PaymentProof { StorageKey = "fixture", ContentType = "image/jpeg", Length = 10 });
        db.AddRange(awaitingProof, awaitingShipping, ordinary);
        await db.SaveChangesAsync();

        var stats = await services.GetRequiredService<IAdminService>().GetDashboardAsync();
        var proofAlert = Assert.Single(stats.Alerts, alert => alert.Url == "/Admin/Orders?attention=proofs");
        var shippingAlert = Assert.Single(stats.Alerts, alert => alert.Url == "/Admin/Orders?attention=shipping");
        Assert.True(proofAlert.Count >= 1);
        Assert.True(shippingAlert.Count >= 1);
        Assert.Equal(StorePermissions.PaymentsReviewProof, proofAlert.Permission);
        Assert.Equal(StorePermissions.OrdersSetShipping, shippingAlert.Permission);

        var orders = services.GetRequiredService<IOrderService>();
        var proofs = await orders.GetPagedAsync(1, 100, search: marker, attention: "proofs");
        var shipping = await orders.GetPagedAsync(1, 100, search: marker, attention: "shipping");
        Assert.Equal(awaitingProof.OrderNumber, Assert.Single(proofs.Items).OrderNumber);
        Assert.Equal(awaitingShipping.OrderNumber, Assert.Single(shipping.Items).OrderNumber);
    }

    [Fact]
    public async Task OperatorDoesNotSeeOrOpenFinancialPanelActions()
    {
        string number;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = new Order
            {
                OrderNumber = "P10-" + Guid.NewGuid().ToString("N")[..20], CustomerName = "Cliente de panel",
                CustomerEmail = "operator-panel@example.test", CustomerPhone = "2025550100",
                DeliveryMethod = Bisuteria.Domain.Enums.DeliveryMethod.Shipping, AddressLine1 = "Calle privada 123",
                AddressLine2 = "Casa privada", City = "Manta", Province = "Manabí",
                DeliverySector = "Sector privado del cliente", DeliveryZoneName = "Zona privada del cliente"
            };
            order.Items.Add(new OrderItem { ProductId = f.ProductId, ProductName = "Pulsera de ensayo", Quantity = 1, UnitPrice = 5 });
            db.Add(order);
            await db.SaveChangesAsync();
            number = order.OrderNumber;
        }

        using var client = await f.Login(StoreRoles.Operator);
        var dashboard = await client.GetStringAsync("/Admin/Dashboard");
        Assert.DoesNotContain("Exportar Excel", dashboard);
        Assert.DoesNotContain("Exportar CSV", dashboard);
        var proofLinkIndex = dashboard.IndexOf("attention=proofs", StringComparison.Ordinal);
        Assert.True(proofLinkIndex < 0, proofLinkIndex < 0 ? "" : dashboard[Math.Max(0, proofLinkIndex - 200)..Math.Min(dashboard.Length, proofLinkIndex + 250)]);
        Assert.DoesNotContain("attention=shipping", dashboard);
        Assert.DoesNotContain("/Admin/Settings", dashboard);
        var detail = await client.GetStringAsync("/Admin/Orders/Details?orderNumber=" + number);
        Assert.DoesNotContain("action=\"/Admin/Orders/UpdatePayment\"", detail);
        Assert.DoesNotContain("action=\"/Admin/Orders/ConfirmShipping\"", detail);
        Assert.Contains("Historial del pedido", detail);
        Assert.Contains("Pedido recibido", detail);
        Assert.Contains("Datos reservados", detail);
        Assert.DoesNotContain("operator-panel@example.test", detail);
        Assert.DoesNotContain("Calle privada 123", detail);
        Assert.DoesNotContain("Casa privada", detail);
        Assert.DoesNotContain("Sector privado del cliente", detail);
        Assert.DoesNotContain("Zona privada del cliente", detail);
        Assert.Contains("Artículos del pedido", detail);
        Assert.Contains("for=\"orderStatus\"", detail);
        Assert.DoesNotContain("paymentStatus", detail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Orders?attention=proofs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Orders?attention=shipping")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Admin/Orders?attention=unknown")).StatusCode);
    }

    [Fact]
    public async Task OwnerCanSeeLocalDeliveryDestinationInAdminOrderDetails()
    {
        string number;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = new Order
            {
                OrderNumber = "P10-" + Guid.NewGuid().ToString("N")[..20], CustomerName = "Cliente local",
                CustomerEmail = "local-delivery@example.test", CustomerPhone = "2025550100",
                DeliveryMethod = Bisuteria.Domain.Enums.DeliveryMethod.LocalDelivery,
                AddressLine1 = "Dirección local de prueba", AddressLine2 = "Referencia ficticia",
                City = "Manta", Province = "Manabí", DeliverySector = "Sector local de prueba",
                DeliveryZoneName = "Zona local de prueba"
            };
            db.Add(order);
            await db.SaveChangesAsync();
            number = order.OrderNumber;
        }

        using var owner = await f.Login(StoreRoles.Owner);
        var html = System.Net.WebUtility.HtmlDecode(await owner.GetStringAsync("/Admin/Orders/Details?orderNumber=" + number));
        Assert.Contains("Lugar de entrega:", html);
        Assert.Contains("Dirección local de prueba", html);
        Assert.Contains("Referencia ficticia", html);
        Assert.Contains("Sector local de prueba", html);
        Assert.Contains("Manta, Manabí", html);
    }

    [Fact]
    public async Task EffectivePermissionsPageShowsAccountGrantsWithoutContactsAndUsesRealLinks()
    {
        string id;
        using (var scope = f.Services.CreateScope())
            id = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("owner@example.test"))!.Id;
        using var developer = await f.Login(StoreRoles.Developer);
        var html = await developer.GetStringAsync("/Developer/Permissions?userId=" + Uri.EscapeDataString(id));
        Assert.Contains(StorePermissions.OrdersSetShipping, html);
        Assert.Contains(id, html);
        Assert.DoesNotContain("owner@example.test", html);
        Assert.DoesNotContain("asp-controller=", html);
        Assert.Contains("href=\"/Developer/Roles\"", html);
        using var owner = await f.Login(StoreRoles.Owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/Developer/Permissions")).StatusCode);
    }

    [Fact]
    public async Task OwnerActivityCannotBeRedirectedToAnotherActorsHistory()
    {
        var ownMarker = "Own." + Guid.NewGuid().ToString("N");
        var otherMarker = "Other." + Guid.NewGuid().ToString("N");
        string otherId;
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = (await users.FindByEmailAsync("owner@example.test"))!;
            var developer = (await users.FindByEmailAsync("developer@example.test"))!;
            otherId = developer.Id;
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(new AuditLog { ActorId = owner.Id, ActorRole = StoreRoles.Owner, Action = ownMarker, Outcome = "200" },
                new AuditLog { ActorId = developer.Id, ActorRole = StoreRoles.Developer, Action = otherMarker, Outcome = "200" });
            await db.SaveChangesAsync();
        }

        using var client = await f.Login(StoreRoles.Owner);
        var html = await client.GetStringAsync("/Admin/Activity?actor=" + Uri.EscapeDataString(otherId));
        Assert.Contains(ownMarker, html);
        Assert.DoesNotContain(otherMarker, html);
        Assert.DoesNotContain("Modo desarrollador", html);
    }
}

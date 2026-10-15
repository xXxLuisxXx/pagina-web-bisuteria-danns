using System.Net;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ContactAndLowStockQueueStoreNotificationsOnceUntilReplenished()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        var original = await settings.GetAsync("ContactEmail");
        await settings.SetAsync("ContactEmail", "store@example.test");
        try
        {
            var name = "Alerta-" + Guid.NewGuid().ToString("N");
            var product = new Product { Name = name, Slug = name, Price = 1, Stock = 1, MinStock = 2, CategoryId = f.CategoryId, IsActive = true };
            db.Add(product); db.Add(new ContactMessage { Name = "Ensayo", Email = "contact@example.test", Message = "Prueba", Subject = "Prueba" });
            await db.SaveChangesAsync();
            product.Stock = 0; await db.SaveChangesAsync();
            Assert.Equal(1, await db.Set<EmailOutbox>().CountAsync(x => x.Subject == "Alerta de stock bajo" && x.TextBody.Contains(name)));
            Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Subject == "Mensaje de contacto nuevo" && x.Recipient == "store@example.test"));
            product.Stock = 10; await db.SaveChangesAsync();
            product.Stock = 1; await db.SaveChangesAsync();
            Assert.Equal(2, await db.Set<EmailOutbox>().CountAsync(x => x.Subject == "Alerta de stock bajo" && x.TextBody.Contains(name)));
            db.Remove(product); await db.SaveChangesAsync();
        }
        finally { await settings.SetAsync("ContactEmail", original); }
    }
    [Fact]
    public async Task OrderEventsPersistNotificationsWithoutWaitingForSmtp()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var order = await orders.CreateAsync(Checkout(), new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Subject == "Confirmación de pedido" && x.TextBody.Contains(order.OrderNumber)));
        Assert.True(await orders.UpdateStatusAsync(order.Id, OrderStatus.Confirmed));
        Assert.True(await orders.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Paid));
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Subject == "Estado de tu pedido" && x.TextBody.Contains(order.OrderNumber)));
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Subject == "Estado de tu pago" && x.TextBody.Contains(order.OrderNumber)));
    }

    [Fact]
    public async Task InitialAdministratorMustChangePasswordAndCannotReuseIt()
    {
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = $"initial-{Guid.NewGuid():N}@example.test";
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["InitialAdmin:Email"] = email, ["InitialAdmin:Password"] = password }).Build();
        await Bisuteria.Infrastructure.Seed.DbInitializer.InitializeAsync(db, users, roles, config);
        var user = (await users.FindByEmailAsync(email))!;
        Assert.True(user.MustChangePassword); Assert.True(user.EmailConfirmed); Assert.True(await users.IsInRoleAsync(user, "Admin"));
        using var client = f.Client();
        await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal("/Account/ChangePassword", (await client.GetAsync("/Admin")).Headers.Location!.OriginalString);
        var same = await AuditFactory.Post(client, "/Account/ChangePassword", new() { ["CurrentPassword"] = password, ["Password"] = password, ["ConfirmPassword"] = password }, "/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        var changed = "Bb2!" + Guid.NewGuid().ToString("N");
        var result = await AuditFactory.Post(client, "/Account/ChangePassword", new() { ["CurrentPassword"] = password, ["Password"] = changed, ["ConfirmPassword"] = changed }, "/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        var reused = await AuditFactory.Post(client, "/Account/ChangePassword", new() { ["CurrentPassword"] = changed, ["Password"] = password, ["ConfirmPassword"] = password }, "/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);
        Assert.Contains("diferente", await reused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PersistentMailRetriesAfterFailure()
    {
        var isolated = new AuditFactory { ProcessEmail = false };
        try
        {
            await isolated.InitializeAsync();
            using var scope = isolated.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<EmailOutbox>().ExecuteUpdateAsync(s => s.SetProperty(x => x.SentAt, DateTime.UtcNow));
            var row = new EmailOutbox { Recipient = "retry@example.test", Subject = "Prueba aislada", TextBody = "Mensaje de ensayo" };
            db.Add(row); await db.SaveChangesAsync();
            var worker = isolated.Services.GetRequiredService<PasswordRecoveryQueue>();
            isolated.Mail.FailNotifications = true;
            Assert.True(await worker.ProcessOneAsync());
            await db.Entry(row).ReloadAsync();
            Assert.Null(row.SentAt); Assert.Equal(1, row.Attempts); Assert.NotNull(row.LastError); Assert.True(row.NextAttemptAt > DateTime.UtcNow);
            row.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
            isolated.Mail.FailNotifications = false;
            Assert.True(await worker.ProcessOneAsync());
            await db.Entry(row).ReloadAsync(); Assert.NotNull(row.SentAt); Assert.Equal(2, row.Attempts);
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }
}

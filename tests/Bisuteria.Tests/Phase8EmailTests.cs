using System.Net.Mail;
using System.Reflection;
using System.Text;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class Phase8EmailTests
{
    [Fact]
    public async Task DevelopmentCapturesMultipartMailEvenWhenSmtpCredentialsExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "BisuteriaAuditMail_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.gmail.com", ["Smtp:User"] = "sender@example.test",
                ["Smtp:Password"] = Guid.NewGuid().ToString("N"), ["Smtp:From"] = "sender@example.test",
                ["PublicBaseUrl"] = "https://example.test"
            }).Build();
            var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance,
                new TestEnvironment { EnvironmentName = "Development", ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
            await sender.SendNotificationAsync("customer@example.test", "Aviso de prueba",
                "<script>alert('x')</script>\nhttps://example.test/Checkout/Confirmation?orderNumber=PRUEBA&accessToken=marcador");

            var file = Assert.Single(Directory.GetFiles(Path.Combine(root, "App_Data", "MailDrop"), "*.eml"));
            var mime = await File.ReadAllTextAsync(file);
            Assert.Contains("multipart/alternative", mime);
            Assert.Contains("text/plain", mime);
            Assert.Contains("text/html", mime);
            var html = ReadHtmlPart(mime);
            Assert.Contains("&lt;script&gt;", html);
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("href=\"https://example.test/Checkout/Confirmation?orderNumber=PRUEBA&amp;accessToken=marcador\"", html);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("child")]
    public async Task DevelopmentMailDropRejectsPublicDirectories(string child)
    {
        var root = Path.Combine(Path.GetTempPath(), "BisuteriaAuditMail_" + Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "wwwroot");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["EmailOutbox:PickupDirectory"] = Path.Combine(webRoot, child) }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance,
            new TestEnvironment { EnvironmentName = "Development", ContentRootPath = root, WebRootPath = webRoot });
        await Assert.ThrowsAsync<SmtpConfigurationException>(() => sender.SendNotificationAsync("customer@example.test", "Ensayo", "Contenido"));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task RealDevelopmentSmtpRequiresExplicitOptInAndValidConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Enabled"] = "true", ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:User"] = "sender@example.test", ["Smtp:From"] = "sender@example.test",
            ["Smtp:Password"] = Guid.NewGuid().ToString("N")
        }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance,
            new TestEnvironment { EnvironmentName = "Development" });
        var error = await Assert.ThrowsAsync<SmtpConfigurationException>(() => sender.SendNotificationAsync("customer@example.test", "Ensayo", "Contenido"));
        Assert.Contains("16 caracteres", error.Message);
    }

    [Fact]
    public void RetryDiagnosticsFitStorageAndNeverCopyProviderMessages()
    {
        var marker = Guid.NewGuid().ToString("N");
        var method = typeof(PasswordRecoveryQueue).GetMethod("GetSafeFailureMessage", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var exception in new Exception[] { new SmtpException(marker), new SmtpConfigurationException(marker + new string('x', 120)), new InvalidOperationException(marker) })
        {
            var safe = (string)method.Invoke(null, [exception, "smtp.gmail.com"])!;
            Assert.InRange(safe.Length, 1, 100);
            Assert.DoesNotContain(marker, safe);
        }
    }

    private static string ReadHtmlPart(string mime)
    {
        var start = mime.IndexOf("Content-Type: text/html", StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0);
        var section = mime[start..];
        var separator = section.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        Assert.True(separator > 0);
        var headers = section[..separator];
        var body = section[(separator + 4)..];
        var boundary = body.IndexOf("\r\n--", StringComparison.Ordinal);
        if (boundary >= 0) body = body[..boundary];
        if (headers.Contains("base64", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetString(Convert.FromBase64String(body));
        if (!headers.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase)) return body;
        body = body.Replace("=\r\n", "");
        using var decoded = new MemoryStream();
        for (var index = 0; index < body.Length; index++)
        {
            if (body[index] == '=' && index + 2 < body.Length && byte.TryParse(body.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var value))
            { decoded.WriteByte(value); index += 2; }
            else decoded.WriteByte((byte)body[index]);
        }
        return Encoding.UTF8.GetString(decoded.ToArray());
    }
}

public partial class AuditTests
{
    [Fact]
    public async Task OutboxPersistsMaximumLengthInvitationRecipient()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipient = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 56) + ".test";
        Assert.Equal(254, recipient.Length);
        var job = new EmailOutbox { Recipient = recipient, Subject = "Ensayo de destinatario", TextBody = "Contenido de prueba" };
        db.Add(job);
        await db.SaveChangesAsync();
        Assert.Equal(recipient, await db.Set<EmailOutbox>().Where(x => x.Id == job.Id).Select(x => x.Recipient).SingleAsync());
    }

    [Fact]
    public async Task OrderEmailsContainAmountsConfiguredPaymentAndNecessaryDeliveryData()
    {
        using var scope = f.Services.CreateScope();
        var services = scope.ServiceProvider;
        var settings = services.GetRequiredService<ISettingService>();
        var values = new Dictionary<string, string>
        {
            ["ContactEmail"] = $"store-{Guid.NewGuid():N}@example.test", ["BankName"] = "Banco de prueba",
            ["BankAccountType"] = "Ahorros", ["BankAccountNumber"] = "CUENTA-DE-PRUEBA", ["BankAccountHolder"] = "Titular de prueba"
        };
        var original = new Dictionary<string, string?>();
        foreach (var key in values.Keys) original[key] = await settings.GetAsync(key);
        try
        {
            foreach (var item in values) await settings.SetAsync(item.Key, item.Value);
            var checkout = Checkout();
            checkout.CheckoutToken = Guid.NewGuid();
            checkout.CustomerEmail = $"order-{Guid.NewGuid():N}@example.test";
            checkout.GuestAccessToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            checkout.PaymentMethod = PaymentMethod.BankTransfer;
            checkout.IsGift = true;
            checkout.GiftRecipientName = "Destinatario de prueba";
            checkout.GiftMessage = "Mensaje de regalo de prueba";
            checkout.Observations = "Observación de prueba";
            var order = await services.GetRequiredService<IOrderService>().CreateAsync(checkout,
                new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 2 }] }, null);
            var db = services.GetRequiredService<AppDbContext>();
            var client = await db.Set<EmailOutbox>().SingleAsync(x => x.Recipient == checkout.CustomerEmail && x.Subject == "Confirmación de pedido");
            Assert.Contains(order.Subtotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " USD", client.TextBody);
            Assert.Contains(order.Items.Single().UnitPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " USD por unidad", client.TextBody);
            Assert.Contains("Total parcial, pendiente de confirmar envío:", client.TextBody);
            Assert.Contains("CUENTA-DE-PRUEBA", client.TextBody);
            Assert.Contains("https://localhost/Checkout/Confirmation?", client.TextBody);
            var store = await db.Set<EmailOutbox>().SingleAsync(x => x.Recipient == values["ContactEmail"] && x.Subject.Contains("Compra pendiente de aprobación"));
            Assert.Contains("Acción requerida: revisa y aprueba esta compra", store.TextBody);
            Assert.Contains(checkout.CustomerName, store.TextBody);
            Assert.Contains(checkout.AddressLine1, store.TextBody);
            Assert.Contains(checkout.CustomerPhone, store.TextBody);
            Assert.Contains(checkout.GiftMessage!, store.TextBody);
            Assert.Contains(checkout.Observations!, store.TextBody);
            Assert.Contains("https://localhost/Admin/Orders/Details?", store.TextBody);
            Assert.DoesNotContain(checkout.GuestAccessToken, store.TextBody);
        }
        finally { foreach (var item in original) await settings.SetAsync(item.Key, item.Value); }
    }

    [Fact]
    public async Task ProofReceiptAndReviewQueueSeparateCustomerNotifications()
    {
        using var scope = f.Services.CreateScope();
        var services = scope.ServiceProvider;
        var orders = services.GetRequiredService<IOrderService>();
        var proofs = services.GetRequiredService<IPaymentProofService>();
        var checkout = Checkout();
        checkout.CheckoutToken = Guid.NewGuid();
        checkout.GuestAccessToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        checkout.CustomerEmail = $"proof-{Guid.NewGuid():N}@example.test";
        checkout.PaymentMethod = PaymentMethod.BankTransfer;
        var order = await orders.CreateAsync(checkout, new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        Assert.True(await orders.ConfirmShippingAsync(order.Id, 0m, "test-owner", StoreRoles.Owner));
        var proofId = await proofs.AddAsync(order.OrderNumber, null, checkout.GuestAccessToken, Guid.NewGuid().ToString("N"), "image/jpeg", 100);
        Assert.NotNull(proofId);
        var db = services.GetRequiredService<AppDbContext>();
        var received = await db.Set<EmailOutbox>().SingleAsync(x => x.Recipient == checkout.CustomerEmail && x.Subject == "Comprobante recibido");
        Assert.Contains(order.OrderNumber, received.TextBody);
        Assert.Equal(PaymentStatus.Pending, (await orders.GetByNumberAsync(order.OrderNumber))!.PaymentStatus);
        Assert.True(await proofs.ReviewAsync(proofId.Value, false, "Motivo de prueba", "test-owner", StoreRoles.Owner));
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == checkout.CustomerEmail && x.Subject == "Revisión de comprobante" && x.TextBody.Contains("Motivo de prueba")));
    }

    [Fact]
    public async Task WelcomePasswordChangeShippingAndRefundEventsQueueExpectedMessages()
    {
        using var scope = f.Services.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var email = $"events-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true };
        Assert.True((await users.CreateAsync(user, AuditFactory.Password)).Succeeded);
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == email && x.Subject == "Bienvenido a Bisuterías Dann’s"));
        Assert.True((await users.ChangePasswordAsync(user, AuditFactory.Password, "Bb2!" + Guid.NewGuid().ToString("N"))).Succeeded);
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == email && x.Subject == "Tu contraseña fue cambiada"));
        var checkout = Checkout();
        checkout.CheckoutToken = Guid.NewGuid();
        checkout.CustomerEmail = email;
        var orders = services.GetRequiredService<IOrderService>();
        var order = await orders.CreateAsync(checkout, new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        Assert.True(await orders.ConfirmShippingAsync(order.Id, 0m, "test-owner", StoreRoles.Owner));
        Assert.True(await orders.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Paid));
        Assert.True(await orders.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Refunded));
        Assert.True(await orders.UpdateStatusAsync(order.Id, OrderStatus.Cancelled));
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == email && x.Subject == "Recargo de envío confirmado" && x.TextBody.Contains(order.OrderNumber)));
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == email && x.Subject == "Estado de tu pago" && x.TextBody.Contains("reembolsado")));
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == email && x.Subject == "Estado de tu pedido" && x.TextBody.Contains("cancelado")));
    }
}

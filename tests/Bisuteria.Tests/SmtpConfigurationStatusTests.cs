using System.Net;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class SmtpConfigurationStatusTests
{
    [Theory]
    [InlineData("not-a-port")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void StatusRejectsPortThatTransportCannotUse(string port)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.test", ["Smtp:From"] = "sender@example.test", ["Smtp:Port"] = port
        }).Build();

        var status = SmtpConfigurationStatus.Describe(configuration, new TestEnvironment());

        Assert.Contains("puerto válido", status);
        Assert.DoesNotContain("Configuración presente", status);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void QueueStatusExplainsWhetherPendingMessagesWillBeProcessed(string? automatic, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["EmailOutbox:AutoProcess"] = automatic }).Build();

        var status = SmtpConfigurationStatus.DescribeQueue(configuration);

        Assert.Contains(expected ? "Procesamiento automático activado" : "permanecen pendientes sin enviarse", status);
    }

    [Fact]
    public void InvalidFlagsProduceSafeDiagnosticInsteadOfThrowing()
    {
        var marker = "invalid-" + Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Smtp:Enabled"] = marker, ["EmailOutbox:AutoProcess"] = marker }).Build();

        var smtp = SmtpConfigurationStatus.Describe(configuration, new TestEnvironment { EnvironmentName = "Development" });
        var queue = SmtpConfigurationStatus.DescribeQueue(configuration);

        Assert.Contains("true o false", smtp);
        Assert.Contains("true o false", queue);
        Assert.DoesNotContain(marker, smtp);
        Assert.DoesNotContain(marker, queue);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("not-an-email", false)]
    [InlineData("owner@example.test", true)]
    public void StoreNotificationStatusChecksRecipientWithoutDisplayingIt(string? recipient, bool valid)
    {
        var status = SmtpConfigurationStatus.DescribeStoreNotifications(recipient);

        Assert.Contains(valid ? "Destinatario de avisos de la tienda configurado" : "Falta un correo válido", status);
        if (!string.IsNullOrWhiteSpace(recipient)) Assert.DoesNotContain(recipient, status);
    }

    [Fact]
    public void StoreNotificationStatusRejectsAddressLongerThanOutboxStorage()
    {
        var recipient = new string('a', 64) + "@" + new string('b', 63) + "." +
            new string('c', 63) + "." + new string('d', 57) + ".test";
        Assert.Equal(255, recipient.Length);

        Assert.Contains("Falta un correo válido", SmtpConfigurationStatus.DescribeStoreNotifications(recipient));
    }

    [Fact]
    public void DevelopmentExplainsThatDisabledSmtpOnlyWritesLocalFiles()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:From"] = "sender@example.test",
            ["Smtp:Enabled"] = "false"
        }).Build();

        var status = SmtpConfigurationStatus.Describe(configuration,
            new TestEnvironment { EnvironmentName = "Development" });

        Assert.Contains("no llegan a Gmail", status);
    }

    [Fact]
    public void GmailStatusConfirmsSettingsWithoutExposingPasswordOrClaimingDelivery()
    {
        var secret = Guid.NewGuid().ToString("N")[..16];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Enabled"] = "true",
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "true",
            ["Smtp:User"] = "sender@example.test",
            ["Smtp:From"] = "sender@example.test",
            ["Smtp:Password"] = secret
        }).Build();

        var status = SmtpConfigurationStatus.Describe(configuration,
            new TestEnvironment { EnvironmentName = "Development" });

        Assert.Contains("falta comprobar la entrega", status);
        Assert.DoesNotContain(secret, status);
    }

    [Fact]
    public void GmailStatusFindsMalformedSecretWithoutRevealingIt()
    {
        var secret = Guid.NewGuid().ToString("N")[..12];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Enabled"] = "true",
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:UseSsl"] = "true",
            ["Smtp:User"] = "sender@example.test",
            ["Smtp:From"] = "sender@example.test",
            ["Smtp:Password"] = secret
        }).Build();

        var status = SmtpConfigurationStatus.Describe(configuration,
            new TestEnvironment { EnvironmentName = "Development" });

        Assert.Contains("16 caracteres", status);
        Assert.DoesNotContain(secret, status);
    }
}

public partial class AuditTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task MissingOrInvalidStoreRecipientDoesNotConsumeLowStockAlert(string invalidRecipient)
    {
        using var scope = f.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await settings.GetAsync("ContactEmail");
        var name = "StockEmail-" + Guid.NewGuid().ToString("N");
        var product = new Product { Name = name, Slug = name, Price = 1, Stock = 1, MinStock = 2, CategoryId = f.CategoryId, IsActive = true };
        try
        {
            await settings.SetAsync("ContactEmail", invalidRecipient);
            db.Add(product);
            await db.SaveChangesAsync();

            Assert.False(product.LowStockNotified);
            Assert.False(await db.Set<EmailOutbox>().AnyAsync(x => x.Subject == "Alerta de stock bajo" && x.TextBody.Contains(name)));

            var recipient = $"stock-{Guid.NewGuid():N}@example.test";
            await settings.SetAsync("ContactEmail", recipient);
            product.Stock = 0;
            await db.SaveChangesAsync();

            Assert.True(product.LowStockNotified);
            Assert.Equal(1, await db.Set<EmailOutbox>().CountAsync(x => x.Recipient == recipient && x.Subject == "Alerta de stock bajo" && x.TextBody.Contains(name)));
            product.Stock = 1;
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Set<EmailOutbox>().CountAsync(x => x.Recipient == recipient && x.Subject == "Alerta de stock bajo" && x.TextBody.Contains(name)));
        }
        finally
        {
            db.Remove(product);
            await db.SaveChangesAsync();
            await settings.SetAsync("ContactEmail", original);
        }
    }

    [Theory]
    [InlineData("/Developer/Home")]
    [InlineData("/Developer/EmailQueue")]
    public async Task DeveloperMailDiagnosticsExplainMissingRecipientAndLocalCaptures(string path)
    {
        using var scope = f.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        var original = await settings.GetAsync("ContactEmail");
        var invalidRecipient = "private-invalid-" + Guid.NewGuid().ToString("N");
        try
        {
            await settings.SetAsync("ContactEmail", invalidRecipient);
            using var developer = await f.Login("Developer");
            var response = await developer.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Falta un correo válido", html);
            Assert.Contains("Procesamiento automático activado", html);
            Assert.Contains("no se reenvían", html);
            Assert.DoesNotContain(invalidRecipient, html);
        }
        finally { await settings.SetAsync("ContactEmail", original); }
    }
}

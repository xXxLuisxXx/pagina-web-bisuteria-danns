using Bisuteria.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task GmailRejectsIncorrectApplicationPasswordLengthBeforeConnecting()
    {
        var secret = Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "true",
            ["Smtp:User"] = "smtp@example.test",
            ["Smtp:Password"] = secret,
            ["Smtp:From"] = "smtp@example.test"
        }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance, new TestEnvironment());

        var error = await Assert.ThrowsAsync<SmtpConfigurationException>(() =>
            sender.SendNotificationAsync("customer@example.test", "Prueba", "Contenido"));

        Assert.Contains("16 caracteres", error.Message);
        Assert.DoesNotContain(secret, error.Message);
    }

    [Fact]
    public async Task GmailRemovesDisplaySpacesBeforeValidatingApplicationPassword()
    {
        var secret = Guid.NewGuid().ToString("N")[..16];
        var spacedSecret = string.Join(" ", Enumerable.Range(0, 4).Select(index => secret.Substring(index * 4, 4)));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "false",
            ["Smtp:User"] = "smtp@example.test",
            ["Smtp:Password"] = spacedSecret,
            ["Smtp:From"] = "smtp@example.test"
        }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance, new TestEnvironment());

        // Una clave de 16 caracteres con separadores pasa la validación y alcanza el control TLS,
        // que falla antes de abrir una conexión de red.
        var error = await Assert.ThrowsAsync<SmtpConfigurationException>(() =>
            sender.SendNotificationAsync("customer@example.test", "Prueba", "Contenido"));

        Assert.Contains("TLS", error.Message);
    }
}

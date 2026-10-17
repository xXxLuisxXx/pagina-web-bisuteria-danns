using Bisuteria.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task GmailRejectsIncorrectApplicationPasswordLengthBeforeConnecting()
    {
        const string secret = "not-a-real-secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "true",
            ["Smtp:User"] = "store@gmail.com",
            ["Smtp:Password"] = secret,
            ["Smtp:From"] = "store@gmail.com"
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
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "false",
            ["Smtp:User"] = "store@gmail.com",
            ["Smtp:Password"] = "abcd efgh ijkl mnop",
            ["Smtp:From"] = "store@gmail.com"
        }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance, new TestEnvironment());

        // Una clave de 16 caracteres con separadores pasa la validación y alcanza el control TLS,
        // que falla antes de abrir una conexión de red.
        var error = await Assert.ThrowsAsync<SmtpConfigurationException>(() =>
            sender.SendNotificationAsync("customer@example.test", "Prueba", "Contenido"));

        Assert.Contains("TLS", error.Message);
    }
}

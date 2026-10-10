using System.Net;
using System.Net.Mail;

namespace Bisuteria.Web.Services;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default);
    Task SendConfirmationAsync(string email, string url, CancellationToken ct = default);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IWebHostEnvironment _environment;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default)
        => SendAsync(email, resetUrl, "Restablece tu contraseña", ct);

    public Task SendConfirmationAsync(string email, string url, CancellationToken ct = default)
        => SendAsync(email, url, "Confirma tu correo", ct);

    private async Task SendAsync(string email, string resetUrl, string purpose, CancellationToken ct)
    {
        var host = _configuration["Smtp:Host"];
        var sender = _configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("SMTP no configurado: no se envió el correo de recuperación.");
                return;
            }

            throw new InvalidOperationException("El correo de recuperación no está configurado.");
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(_configuration["Smtp:UseSsl"], out var useSsl) ? useSsl : true
        };

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(sender, email)
        {
            Subject = purpose + " | Bisuterias Dann's",
            Body = $"<p><a href=\"{WebUtility.HtmlEncode(resetUrl)}\">{WebUtility.HtmlEncode(purpose)}</a></p><p>Si no hiciste esta solicitud, ignora este correo.</p>",
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, ct);
    }
}

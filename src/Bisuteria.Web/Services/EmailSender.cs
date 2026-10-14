using System.Net;
using System.Net.Mail;

namespace Bisuteria.Web.Services;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default);
    Task SendConfirmationAsync(string email, string url, CancellationToken ct = default);
    Task SendNotificationAsync(string email, string subject, string text, CancellationToken ct = default);
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

    public Task SendNotificationAsync(string email, string subject, string text, CancellationToken ct = default)
        => DeliverAsync(email, subject, text, ct);

    private async Task SendAsync(string email, string resetUrl, string purpose, CancellationToken ct)
        => await DeliverAsync(email, purpose, $"{purpose}: {resetUrl}\nSi no hiciste esta solicitud, ignora este correo.", ct);

    private async Task DeliverAsync(string email, string purpose, string text, CancellationToken ct)
    {
        var host = _configuration["Smtp:Host"];
        var sender = _configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("SMTP no configurado: no se envió el correo de recuperación.");
                throw new InvalidOperationException("SMTP no configurado.");
            }

            throw new InvalidOperationException("El correo de recuperación no está configurado.");
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(_configuration["Smtp:UseSsl"], out var useSsl) ? useSsl : true
        };

        var username = _configuration["Smtp:User"] ?? _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(sender, email)
        {
            Subject = purpose + " | Bisuterias Dann's",
            Body = text,
            IsBodyHtml = false
        };

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, System.Text.Encoding.UTF8, "text/plain"));
        var safeText = WebUtility.HtmlEncode(text).Replace("\n", "<br>");
        var logo = Uri.TryCreate(_configuration["PublicBaseUrl"], UriKind.Absolute, out var origin) && origin.Scheme == "https"
            ? $"<img src=\"{WebUtility.HtmlEncode(new Uri(origin, "/img/logo-danns.svg").AbsoluteUri)}\" alt=\"Bisuterías Dann’s\" width=\"120\">" : "";
        var html = $"<!doctype html><html lang=\"es\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><body style=\"margin:0;background:#f4f3f0;font-family:Arial,sans-serif;color:#222\"><div style=\"max-width:600px;margin:24px auto;padding:24px;background:white;border-radius:20px\">{logo}<h1 style=\"color:#806615;font-size:24px\">Bisuterías Dann’s</h1><h2>{WebUtility.HtmlEncode(purpose)}</h2><p style=\"line-height:1.6;overflow-wrap:anywhere\">{safeText}</p><hr><p>Correo transaccional de tu cuenta o pedido. Nunca te solicitaremos tu contraseña por correo.</p></div></body></html>";
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, "text/html"));

        await client.SendMailAsync(message, ct);
    }
}

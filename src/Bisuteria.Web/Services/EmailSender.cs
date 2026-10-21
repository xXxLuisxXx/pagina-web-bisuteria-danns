using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace Bisuteria.Web.Services;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default);
    Task SendConfirmationAsync(string email, string url, CancellationToken ct = default);
    Task SendNotificationAsync(string email, string subject, string text, CancellationToken ct = default);
}

public sealed class SmtpConfigurationException(string message) : InvalidOperationException(message)
{
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
        // El desarrollo captura correo local incluso si hay credenciales SMTP guardadas.
        // El envío real se habilita de forma explícita y nunca desde las pruebas.
        if (_environment.IsDevelopment() && !_configuration.GetValue("Smtp:Enabled", false))
        {
            var directory = GetPickupDirectory();
            Directory.CreateDirectory(directory);
            var from = _configuration["Smtp:From"];
            using var localMessage = CreateMessage(string.IsNullOrWhiteSpace(from) ? "no-reply@example.test" : from, email, purpose, text);
            using var pickupClient = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = directory,
                EnableSsl = false
            };
            await pickupClient.SendMailAsync(localMessage, ct);
            _logger.LogInformation("Correo de desarrollo guardado en archivo local.");
            return;
        }

        var host = _configuration["Smtp:Host"];
        var sender = _configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("SMTP no configurado: no se envió el correo.");
                throw new SmtpConfigurationException("Configura Smtp:Host y Smtp:From para habilitar el correo.");
            }

            throw new SmtpConfigurationException("El correo no está configurado.");
        }

        var portValue = _configuration["Smtp:Port"];
        if (!string.IsNullOrWhiteSpace(portValue) && (!int.TryParse(portValue, out var parsedPort) || parsedPort is < 1 or > 65535))
            throw new SmtpConfigurationException("Smtp:Port debe ser un puerto válido entre 1 y 65535.");
        var port = string.IsNullOrWhiteSpace(portValue) ? 587 : int.Parse(portValue);

        var username = _configuration["Smtp:User"] ?? _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
            throw new SmtpConfigurationException("Configura Smtp:User y Smtp:Password juntos para autenticar el servidor de correo.");

        if (host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new SmtpConfigurationException("Gmail requiere Smtp:User y una contraseña de aplicación.");

            // Google muestra la contraseña de aplicación en grupos; los espacios no forman parte de ella.
            password = new string(password!.Where(character => !char.IsWhiteSpace(character)).ToArray());
            if (password.Length != 16)
                throw new SmtpConfigurationException("La contraseña de aplicación de Gmail debe tener 16 caracteres. No uses la contraseña normal de Google.");
            if (_configuration.GetValue("Smtp:UseSsl", true) is false)
                throw new SmtpConfigurationException("Gmail requiere TLS; configura Smtp:UseSsl en true.");
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(_configuration["Smtp:UseSsl"], out var useSsl) ? useSsl : true
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(username, password);

        using var message = CreateMessage(sender, email, purpose, text);
        await client.SendMailAsync(message, ct);
    }

    private MailMessage CreateMessage(string sender, string email, string purpose, string text)
    {
        var message = new MailMessage(sender, email)
        {
            Subject = purpose + " | Bisuterias Dann's",
            Body = text,
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = false
        };

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, System.Text.Encoding.UTF8, "text/plain"));
        var safeText = FormatHtmlText(text);
        var logo = Uri.TryCreate(_configuration["PublicBaseUrl"], UriKind.Absolute, out var origin) && origin.Scheme == "https"
            ? $"<img src=\"{WebUtility.HtmlEncode(new Uri(origin, "/img/logo-danns.svg").AbsoluteUri)}\" alt=\"Bisuterías Dann’s\" width=\"120\">" : "";
        var html = $"<!doctype html><html lang=\"es\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><body style=\"margin:0;background:#f4f3f0;font-family:Arial,sans-serif;color:#222\"><div style=\"max-width:600px;margin:24px auto;padding:24px;background:white;border-radius:20px\">{logo}<h1 style=\"color:#806615;font-size:24px\">Bisuterías Dann’s</h1><h2>{WebUtility.HtmlEncode(purpose)}</h2><p style=\"line-height:1.6;overflow-wrap:anywhere\">{safeText}</p><hr><p>Correo transaccional de tu cuenta o pedido. Nunca te solicitaremos tu contraseña por correo.</p></div></body></html>";
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, "text/html"));

        return message;
    }

    private string FormatHtmlText(string text)
    {
        // Solo los enlaces del origen configurado se convierten en enlaces HTML.
        // Los comentarios y datos introducidos por clientes siempre se codifican.
        Uri.TryCreate(_configuration["PublicBaseUrl"], UriKind.Absolute, out var origin);
        var html = new StringBuilder();
        var offset = 0;
        foreach (Match match in Regex.Matches(text, @"https://[^\s<>""']+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            html.Append(WebUtility.HtmlEncode(text[offset..match.Index]));
            var safeUrl = WebUtility.HtmlEncode(match.Value);
            if (origin?.Scheme == Uri.UriSchemeHttps && Uri.TryCreate(match.Value, UriKind.Absolute, out var target) &&
                string.IsNullOrEmpty(target.UserInfo) && string.Equals(target.Authority, origin.Authority, StringComparison.OrdinalIgnoreCase))
                html.Append($"<a href=\"{safeUrl}\">{safeUrl}</a>");
            else
                html.Append(safeUrl);
            offset = match.Index + match.Length;
        }
        html.Append(WebUtility.HtmlEncode(text[offset..]));
        return html.ToString().Replace("\n", "<br>");
    }

    private string GetPickupDirectory()
    {
        var configured = _configuration["EmailOutbox:PickupDirectory"];
        var directory = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(_environment.ContentRootPath, "App_Data", "MailDrop")
            : Path.IsPathRooted(configured) ? configured : Path.Combine(_environment.ContentRootPath, configured));
        var webRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot") : _environment.WebRootPath);
        var relative = Path.GetRelativePath(webRoot, directory);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
            throw new SmtpConfigurationException("La carpeta de correo debe permanecer fuera de wwwroot.");
        return directory;
    }
}

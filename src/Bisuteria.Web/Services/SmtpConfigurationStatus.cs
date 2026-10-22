namespace Bisuteria.Web.Services;

/// <summary>Diagnóstico seguro de configuración SMTP; nunca revela valores secretos.</summary>
public static class SmtpConfigurationStatus
{
    public static string Describe(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            if (!TryReadFlag(configuration, "Smtp:Enabled", false, out var enabled))
                return "Smtp:Enabled debe ser true o false.";
            if (!enabled)
                return "Desactivado en Development: los correos nuevos se guardan localmente y no llegan a Gmail.";
        }

        var host = configuration["Smtp:Host"];
        var sender = configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
            return "Faltan el servidor SMTP o el correo remitente.";

        var portValue = configuration["Smtp:Port"];
        if (!string.IsNullOrWhiteSpace(portValue) &&
            (!int.TryParse(portValue, out var port) || port is < 1 or > 65535))
            return "Smtp:Port debe ser un puerto válido entre 1 y 65535.";

        var username = configuration["Smtp:User"] ?? configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
            return "Completa usuario y contraseña SMTP juntos.";

        if (host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(username)) return "Gmail requiere usuario y contraseña de aplicación.";
            var compactPassword = new string(password!.Where(character => !char.IsWhiteSpace(character)).ToArray());
            if (compactPassword.Length != 16) return "La contraseña de aplicación de Gmail debe tener 16 caracteres.";
            if (!TryReadFlag(configuration, "Smtp:UseSsl", true, out var useSsl))
                return "Smtp:UseSsl debe ser true o false.";
            if (!useSsl) return "Gmail requiere TLS habilitado.";
        }

        return "Configuración presente; falta comprobar la entrega con el proveedor.";
    }

    public static string DescribeQueue(IConfiguration configuration)
    {
        if (!TryReadFlag(configuration, "EmailOutbox:AutoProcess", true, out var automatic))
            return "EmailOutbox:AutoProcess debe ser true o false; revisa la configuración de la cola.";
        return automatic
            ? "Procesamiento automático activado. Los fallos se reintentan hasta ocho veces."
            : "Procesamiento automático desactivado: los mensajes permanecen pendientes sin enviarse.";
    }

    public static string DescribeStoreNotifications(string? recipient)
        => string.IsNullOrWhiteSpace(recipient) || recipient.Length > 254 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(recipient)
            ? "Falta un correo válido para los avisos de la tienda. Configúralo en Configuración; los avisos no se generan mientras falte."
            : "Destinatario de avisos de la tienda configurado; es independiente del remitente SMTP.";

    private static bool TryReadFlag(IConfiguration configuration, string key, bool fallback, out bool value)
    {
        var configured = configuration[key];
        value = fallback;
        return configured is null || bool.TryParse(configured, out value);
    }
}

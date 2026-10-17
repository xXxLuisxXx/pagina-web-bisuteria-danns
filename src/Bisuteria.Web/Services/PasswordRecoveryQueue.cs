using System.Text;
using System.Net.Mail;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Services;

// Cola durable: la solicitud HTTP guarda trabajo y nunca espera al servidor SMTP.
public sealed class PasswordRecoveryQueue(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<PasswordRecoveryQueue> logger, IWebHostEnvironment environment, IDataProtectionProvider dataProtection) : BackgroundService
{
    private const string InviteEmailProtectionPurpose = "BisuteriasDanns.StaffInvitation.Email.v1";
    public async Task EnqueueAsync(string email, bool confirm = false)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new EmailOutbox { Recipient = email, Kind = confirm ? "Confirmation" : "Recovery" });
        await db.SaveChangesAsync();
    }

    public async Task<bool> ProcessOneAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var id = await db.Set<EmailOutbox>().Where(x => x.SentAt == null && x.Attempts < 8 && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        if (id is null) return false;
        // Reserva atómica con vencimiento. Recuperable después de un cierre inesperado.
        var claimed = await db.Set<EmailOutbox>().Where(x => x.Id == id && x.SentAt == null && x.NextAttemptAt <= now && x.Attempts < 8)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, now.AddMinutes(5)).SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
        if (claimed == 0) return true;
        var job = await db.Set<EmailOutbox>().SingleAsync(x => x.Id == id, ct);
        try
        {
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            if (job.Kind is "Confirmation" or "Recovery")
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByEmailAsync(job.Recipient);
                if (user is { IsActive: true })
                {
                    if (!Uri.TryCreate(configuration["PublicBaseUrl"], UriKind.Absolute, out var origin) || origin.Scheme != "https") throw new InvalidOperationException("Origen HTTPS no configurado.");
                    var confirm = job.Kind == "Confirmation";
                    var raw = confirm ? await users.GenerateEmailConfirmationTokenAsync(user) : await users.GeneratePasswordResetTokenAsync(user);
                    var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
                    var url = QueryHelpers.AddQueryString(new Uri(origin, confirm ? "/Account/ConfirmEmail" : "/Account/ResetPassword").AbsoluteUri, new Dictionary<string,string?> { ["email"] = user.Email, ["token"] = token });
                    if (confirm) await sender.SendConfirmationAsync(job.Recipient, url, ct);
                    else await sender.SendPasswordResetAsync(job.Recipient, url, ct);
                }
            }
            else
            {
                var text = job.Kind == "StaffInvitation"
                    ? dataProtection.CreateProtector(InviteEmailProtectionPurpose).Unprotect(job.TextBody)
                    : job.TextBody;
                await sender.SendNotificationAsync(job.Recipient, job.Subject, text, ct);
            }
            job.SentAt = DateTime.UtcNow;
            job.LastError = null;
            logger.LogInformation("Correo de cola {Id} procesado, intento {Attempt}.", job.Id, job.Attempts);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            job.LastError = GetSafeFailureMessage(ex, configuration["Smtp:Host"]);
            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, job.Attempts - 1)));
            if (ex is SmtpException smtpException)
                logger.LogWarning("Entrega de correo {Id} fallida, intento {Attempt}; estado SMTP {StatusCode}.", job.Id, job.Attempts, smtpException.StatusCode);
            else
                logger.LogWarning("Entrega de correo {Id} fallida, intento {Attempt}; tipo {ExceptionType}.", job.Id, job.Attempts, ex.GetType().Name);
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string GetSafeFailureMessage(Exception exception, string? host)
    {
        if (exception is SmtpConfigurationException configurationException)
            return configurationException.Message;

        if (exception is SmtpException)
            return string.Equals(host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
                ? "Gmail rechazó o no pudo completar el envío. Revisa Smtp:User y la contraseña de aplicación de 16 caracteres."
                : "El servidor SMTP rechazó o no pudo completar el envío. Revisa host, puerto, TLS y credenciales.";

        return "Entrega fallida; revisar proveedor de correo.";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (configuration.GetValue("EmailOutbox:AutoProcess", true) && await ProcessOneAsync(stoppingToken)) continue;
                await Task.Delay(environment.IsEnvironment("Testing") ? 100 : 5000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Cola de correo no disponible; se reintentará sin detener la aplicación."); }
        }
    }
}

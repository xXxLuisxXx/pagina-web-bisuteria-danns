using System.Text;
using System.Threading.Channels;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Bisuteria.Web.Services;

/// <summary>Cola limitada en memoria. La petición HTTP no consulta cuentas ni espera SMTP.</summary>
public sealed class PasswordRecoveryQueue : BackgroundService
{
    private readonly Channel<(string Email, bool Confirm)> _requests = Channel.CreateBounded<(string, bool)>(100);
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordRecoveryQueue> _logger;

    public PasswordRecoveryQueue(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<PasswordRecoveryQueue> logger)
        => (_scopes, _configuration, _logger) = (scopes, configuration, logger);

    public void Enqueue(string email, bool confirm = false)
    {
        if (!_requests.Writer.TryWrite((email, confirm))) _logger.LogWarning("Cola de correo llena; solicitud descartada.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _requests.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByEmailAsync(request.Email);
                if (user is not { IsActive: true }) continue;
                if (!Uri.TryCreate(_configuration["PublicBaseUrl"], UriKind.Absolute, out var origin) || origin.Scheme != "https")
                {
                    _logger.LogError("Configura PublicBaseUrl con un origen HTTPS válido.");
                    continue;
                }
                var rawToken = request.Confirm ? await users.GenerateEmailConfirmationTokenAsync(user) : await users.GeneratePasswordResetTokenAsync(user);
                var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                var url = QueryHelpers.AddQueryString(new Uri(origin, request.Confirm ? "/Account/ConfirmEmail" : "/Account/ResetPassword").AbsoluteUri,
                    new Dictionary<string, string?> { ["email"] = user.Email, ["token"] = token });
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                if (request.Confirm) await sender.SendConfirmationAsync(user.Email!, url, stoppingToken);
                else await sender.SendPasswordResetAsync(user.Email!, url, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { _logger.LogError("No se pudo procesar una recuperación de contraseña; revisa el proveedor SMTP."); }
        }
    }
}

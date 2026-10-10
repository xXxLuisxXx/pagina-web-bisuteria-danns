using System.Net;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace Bisuteria.Web.Security;

public static class SecurityConfiguration
{
    public static void AddStoreSecurity(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        if (builder.Environment.IsProduction())
        {
            var hosts = config["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(hosts) || hosts.Split(';').Any(h => h.Trim() == "*"))
                throw new InvalidOperationException("Configura AllowedHosts con los dominios de producción.");
            if (!Uri.TryCreate(config["PublicBaseUrl"], UriKind.Absolute, out var origin) || origin.Scheme != "https")
                throw new InvalidOperationException("Configura PublicBaseUrl con el origen HTTPS de producción.");
            if (string.IsNullOrWhiteSpace(config["DataProtection:KeysPath"]))
                throw new InvalidOperationException("Configura DataProtection:KeysPath en almacenamiento persistente protegido.");
        }
        var protection = builder.Services.AddDataProtection().SetApplicationName("BisuteriasDanns");
        if (config["DataProtection:KeysPath"] is { Length: > 0 } path)
            protection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(path)));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var address in config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(address));
        });
        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = config.GetValue("Security:HstsIncludeSubDomains", true);
            options.Preload = config.GetValue("Security:HstsPreload", true);
        });
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32 * 1024 * 1024);
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 32 * 1024 * 1024;
            options.ValueLengthLimit = 20_000;
            options.ValueCountLimit = 256;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var controller = context.GetRouteValue("controller")?.ToString()?.ToLowerInvariant() ?? "";
                var action = context.GetRouteValue("action")?.ToString()?.ToLowerInvariant() ?? "";
                var group = controller == "account" ? action : controller;
                var limit = group switch { "login" => 10, "register" => 5, "forgotpassword" => 5, "contact" => 5, "checkout" => 10, "cart" => 40, "customizer" => 20, "catalog" => 90, _ => 120 };
                if (builder.Environment.IsEnvironment("Testing") && !config.GetValue("Security:TestRateLimits", false))
                    return RateLimitPartition.GetNoLimiter("testing");
                var key = $"{group}:{context.Connection.RemoteIpAddress}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });
        });
    }

    public static async Task Headers(HttpContext context, Func<Task> next)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        context.Items["CspNonce"] = nonce;
        context.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; script-src-attr 'none'; style-src 'self' https://fonts.googleapis.com 'nonce-{nonce}'; style-src-attr 'none'; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        await next();
    }
}

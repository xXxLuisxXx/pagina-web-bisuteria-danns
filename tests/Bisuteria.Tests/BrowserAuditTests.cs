using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>Esta auditoría abre un servidor HTTPS real y crea únicamente datos ficticios temporales.</summary>
public sealed class BrowserAuditTests
{
    [BrowserAuditFact]
    [Trait("Category", "Browser")]
    public async Task BrowserFlowsAndAccessibilityUseDisposableDatabase()
    {
        var factory = new AuditFactory { ProcessEmail = false };
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps()));
        try
        {
            await factory.InitializeAsync();
            var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.Single(value => value.StartsWith("https://", StringComparison.Ordinal));
            factory.Services.GetRequiredService<IConfiguration>()["PublicBaseUrl"] = address;

            string developerKey;
            string ownerKey;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(factory.DatabaseName, db.Database.GetDbConnection().Database);
                Assert.Matches("^BisuteriaAudit_[0-9a-f]{32}$", factory.DatabaseName);
                var settings = await db.SiteSettings.ToDictionaryAsync(setting => setting.Key);
                foreach (var (key, value) in new Dictionary<string, string>
                {
                    ["PaymentBankTransferEnabled"] = "true", ["BankName"] = "Banco de prueba",
                    ["BankAccountType"] = "Cuenta de prueba", ["BankAccountNumber"] = "SOLO-PRUEBAS",
                    ["BankAccountHolder"] = "Titular ficticio", ["ContactEmail"] = "store@example.test"
                })
                {
                    if (settings.TryGetValue(key, out var setting)) setting.Value = value;
                    else db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
                }
                db.BlogPosts.Add(new BlogPost
                {
                    Title = "Cómo cuidar tus accesorios", Slug = "browser-audit", Excerpt = "Consejos de ejemplo para la auditoría.",
                    Content = "Conserva tus accesorios secos y limpios. Este contenido es ficticio y solo existe en la base temporal.",
                    CoverImage = "/img/placeholder.svg", IsPublished = true, PublishedAt = DateTime.UtcNow
                });
                db.GalleryImages.Add(new GalleryImage { Title = "Diseño de prueba", Description = "Contenido ficticio de auditoría", Category = "Prueba", ImageUrl = "/img/placeholder.svg" });
                await db.SaveChangesAsync();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                developerKey = (await users.GetAuthenticatorKeyAsync((await users.FindByEmailAsync("developer@example.test"))!))!;
                ownerKey = (await users.GetAuthenticatorKeyAsync((await users.FindByEmailAsync("owner@example.test"))!))!;
            }

            var root = FindRepositoryRoot();
            var phase = Environment.GetEnvironmentVariable("BROWSER_AUDIT_PHASE") == "after" ? "after" : "before";
            var start = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("NODE_PATH_EXECUTABLE") ?? "node",
                WorkingDirectory = root,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            start.ArgumentList.Add(Path.Combine(root, "tests", "visual", "browser-audit.cjs"));
            start.Environment["AUDIT_BASE_URL"] = address;
            start.Environment["AUDIT_PHASE"] = phase;
            start.Environment["AUDIT_PASSWORD"] = AuditFactory.Password;
            start.Environment["AUDIT_DEVELOPER_TOTP"] = developerKey;
            start.Environment["AUDIT_OWNER_TOTP"] = ownerKey;
            start.Environment["AUDIT_PRODUCT_ID"] = factory.ProductId.ToString();
            start.Environment["AUDIT_CATEGORY_ID"] = factory.CategoryId.ToString();
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new TimeoutException("La auditoría de navegador superó 20 minutos."); }
            var standardOutput = await output;
            var standardError = await errors;
            Assert.True(process.ExitCode == 0, $"La auditoría de navegador falló. Revisa audit/browser/{phase}/report.json.\n{standardOutput}\n{standardError}");

            using var verification = factory.Services.CreateScope();
            var verify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await verify.Orders.AnyAsync(order => order.CustomerEmail == "browser-order@example.test"));
            Assert.True(await verify.PaymentProofs.AnyAsync());
            if (Environment.GetEnvironmentVariable("BROWSER_AUDIT_QUICK") != "1")
            {
                Assert.True(await verify.ContactMessages.AnyAsync(message => message.Email == "browser-contact@example.test"));
                Assert.NotNull(await verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("browser-register@example.test"));
            }
        }
        finally { await ((IAsyncLifetime)factory).DisposeAsync(); }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bisuteria.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("No se encontró la raíz del proyecto para la auditoría.");
    }
}

public sealed class BrowserAuditFactAttribute : FactAttribute
{
    public BrowserAuditFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BROWSER_AUDIT") != "1")
            Skip = "Auditoría de navegador optativa: ejecutar con BROWSER_AUDIT=1 y dependencias npm instaladas.";
    }
}

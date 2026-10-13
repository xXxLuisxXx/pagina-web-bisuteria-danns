using System.Net;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task AdminUsersAcceptsAnAccountWithoutDisplayName()
    {
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"empty-{Guid.NewGuid():N}@example.test";
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = email, Email = email, IsActive = true }, "Aa1!" + Guid.NewGuid().ToString("N"))).Succeeded);
        using var admin = await f.Login("Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/Users")).StatusCode);
    }
    [Fact]
    public async Task PasswordAloneCannotAuthenticateAnAccountWithTwoFactor()
    {
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"totp-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true, EmailConfirmed = true };
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 2))!.ToArray();
        using var client = f.Client();
        var result = await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.StartsWith("/Account/TwoFactor", result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        var invalid = await AuditFactory.Post(client, "/Account/TwoFactor", new() { ["Code"] = "invalid" }, "/Account/TwoFactor");
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
        var recovery = await AuditFactory.Post(client, "/Account/TwoFactor", new() { ["Code"] = codes[0], ["Recovery"] = "true", ["ReturnUrl"] = "https://example.test" }, "/Account/TwoFactor");
        Assert.Equal("/", recovery.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Profile")).StatusCode);
        using var verificationScope = f.Services.CreateScope();
        var verificationUsers = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.False((await verificationUsers.RedeemTwoFactorRecoveryCodeAsync((await verificationUsers.FindByEmailAsync(email))!, codes[0])).Succeeded);
    }

    [Fact]
    public async Task AuthenticatorEnrollmentRequiresPasswordAndValidTotp()
    {
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"enroll-{Guid.NewGuid():N}@example.test";
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        using var client = f.Client();
        await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
        var denied = await AuditFactory.Post(client, "/Account/Authenticator", new() { ["Password"] = "incorrecta" }, "/Account/Authenticator");
        Assert.DoesNotContain("Clave privada:", await denied.Content.ReadAsStringAsync());
        var prepared = await AuditFactory.Post(client, "/Account/Authenticator", new() { ["Password"] = password }, "/Account/Authenticator");
        Assert.Equal("no-store,no-cache", prepared.Headers.CacheControl!.ToString().Replace(" ", ""));
        var html = await prepared.Content.ReadAsStringAsync();
        var key = System.Text.RegularExpressions.Regex.Match(html, "Clave privada: <code>([^<]+)</code>").Groups[1].Value;
        Assert.NotEmpty(key);
        var enabled = await AuditFactory.Post(client, "/Account/Authenticator", new() { ["Password"] = password, ["Code"] = Totp(key) }, "/Account/Authenticator");
        Assert.Contains("cada uno sirve", await enabled.Content.ReadAsStringAsync());
        using var freshScope = f.Services.CreateScope();
        var freshUsers = freshScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await freshUsers.GetTwoFactorEnabledAsync((await freshUsers.FindByEmailAsync(email))!));
        using var another = f.Client();
        await AuditFactory.Post(another, "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
        var login = await AuditFactory.Post(another, "/Account/TwoFactor", new() { ["Code"] = Totp(key) }, "/Account/TwoFactor");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await another.GetAsync("/Account/Profile")).StatusCode);
    }

    // Algoritmo de referencia para generar un código solo con la clave efímera de la prueba.
    private static string Totp(string key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0; var bits = 0;
        foreach (var c in key) { buffer = (buffer << 5) | alphabet.IndexOf(c); bits += 5; if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); } }
        var counter = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30));
        var hash = System.Security.Cryptography.HMACSHA1.HashData(bytes.ToArray(), counter);
        var offset = hash[^1] & 15;
        var value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }
}

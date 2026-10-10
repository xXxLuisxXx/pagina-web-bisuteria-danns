using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Bisuteria.Application.Services;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task CspUsesUniqueNoncesAndNoInlineEventHandlers()
    {
        using var client = f.Client();
        var response = await client.GetAsync("/");
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.DoesNotContain("unsafe-inline", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        var nonce = Regex.Match(policy, "'nonce-([^']+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotMatch("\\son(?:click|submit|error|change)=", html);
        foreach (Match tag in Regex.Matches(html, "<script[^>]*>")) Assert.Contains("nonce=\"" + nonce + "\"", WebUtility.HtmlDecode(tag.Value));
        var second = await client.GetAsync("/");
        Assert.NotEqual(policy, second.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task HoneypotRejectsContactWithoutSaving()
    {
        using var client = f.Client();
        var result = await AuditFactory.Post(client, "/Contact", new()
        { ["Name"] = "Robot", ["Email"] = "robot@example.test", ["Subject"] = "Ensayo", ["Message"] = "Mensaje de prueba automatizado", ["Website"] = "spam.example" });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var scope = f.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().ContactMessages.AnyAsync(m => m.Email == "robot@example.test"));
    }

    [Fact]
    public async Task PasswordChangeRevokesOtherSessions()
    {
        var email = $"change-{Guid.NewGuid():N}@example.test";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new() { UserName = email, Email = email, IsActive = true }, AuditFactory.Password)).Succeeded);
        }
        using var first = f.Client(); using var second = f.Client();
        foreach (var client in new[] { first, second })
            Assert.Equal(HttpStatusCode.Redirect, (await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = AuditFactory.Password })).StatusCode);
        var response = await AuditFactory.Post(first, "/Account/ChangePassword", new()
        { ["CurrentPassword"] = AuditFactory.Password, ["Password"] = "Changed-Only!12345", ["ConfirmPassword"] = "Changed-Only!12345" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/Account/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await second.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task ConfirmationRequiresValidTokenAndPost()
    {
        var email = $"confirm-{Guid.NewGuid():N}@example.test";
        string token;
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, IsActive = true };
            Assert.True((await users.CreateAsync(user, AuditFactory.Password)).Succeeded);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        }
        using var client = f.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Account/ConfirmEmail?email={email}&token={token}")).StatusCode);
        using (var scope = f.Services.CreateScope()) Assert.False((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!.EmailConfirmed);
        Assert.Equal(HttpStatusCode.Redirect, (await AuditFactory.Post(client, "/Account/ConfirmEmail", new() { ["Email"] = email, ["Token"] = token })).StatusCode);
        using var verify = f.Services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!.EmailConfirmed);
    }

    [Theory]
    [InlineData("=1+1")][InlineData(" +SUM(A1)")][InlineData("@test")][InlineData("-1")]
    public void SpreadsheetFormulaPrefixesAreNeutralized(string value) => Assert.StartsWith("'", SpreadsheetText.Safe(value));

    [Fact]
    public async Task AdministrativeRequestsHaveAuditMetadataOnly()
    {
        using var admin = await f.Login("Admin");
        await AuditFactory.Post(admin, "/Admin/Settings/Save", new() { ["values[ShippingCost]"] = "0" });
        using var scope = f.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AuditLog>().Where(l => l.Action == "Settings/Save").ToListAsync();
        Assert.NotEmpty(logs);
        Assert.All(logs, log => { Assert.NotEmpty(log.ActorId); Assert.DoesNotContain("Password", log.Action); });
    }

    [Fact]
    public async Task RateLimitBlocksExcessLoginRequests()
    {
        using var host = f.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:TestRateLimits"] = "true" })));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        HttpResponseMessage? response = null;
        for (var i = 0; i < 11; i++) response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }
}

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Bisuteria.Application.Services;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task SensitiveActionsRequireFreshLoginAndUseOnlyLocalReturnUrls()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = f.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Referrer = new Uri("https://evil.example/steal");
        var login = await AuditFactory.Post(client, "/Account/Login", new()
        { ["Email"] = "developer@example.test", ["Password"] = AuditFactory.Password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        clock.Advance(TimeSpan.FromMinutes(16));
        var response = await AuditFactory.Post(client, "/Developer/Roles/Save", new() { ["role"] = "Operator" }, "/Developer/Roles");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login?reauthenticationRequired=true&returnUrl=%2FDeveloper", response.Headers.Location!.OriginalString);
        var prompt = await client.GetAsync(response.Headers.Location);
        Assert.Contains("Confirma tu identidad", await prompt.Content.ReadAsStringAsync());
        Assert.DoesNotContain("evil.example", response.Headers.Location.OriginalString);
        var renewedSession = await AuditFactory.Post(client, "/Account/Login", new()
        { ["Email"] = "developer@example.test", ["Password"] = AuditFactory.Password, ["ReturnUrl"] = "/Developer/Roles" });
        Assert.Equal(HttpStatusCode.Redirect, renewedSession.StatusCode);
        Assert.Equal("/Developer/Roles", renewedSession.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LoginLockoutDurationIncreasesAfterRepeatedLockouts()
    {
        var email = $"lockout-{Guid.NewGuid():N}@example.test";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser { UserName = email, Email = email, IsActive = true }, AuditFactory.Password)).Succeeded);
        }

        using var client = f.Client();
        for (var attempt = 0; attempt < 5; attempt++)
            await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = "Incorrect-Password!123" });

        DateTimeOffset? firstLockout;
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            firstLockout = await users.GetLockoutEndDateAsync(user!);
            Assert.InRange((firstLockout!.Value - DateTimeOffset.UtcNow).TotalMinutes, 4.8, 5.1);
            Assert.Contains((await users.GetClaimsAsync(user!)), claim => claim.Type == "security:lockout-level" && claim.Value == "0");
            await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddSeconds(-1));
            await users.ResetAccessFailedCountAsync(user!);
        }

        for (var attempt = 0; attempt < 5; attempt++)
            await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = "Incorrect-Password!123" });

        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            var secondLockout = await users.GetLockoutEndDateAsync(user!);
            Assert.True(secondLockout > firstLockout!.Value.AddMinutes(4));
            Assert.InRange((secondLockout!.Value - DateTimeOffset.UtcNow).TotalMinutes, 9.8, 10.1);
            Assert.Contains((await users.GetClaimsAsync(user!)), claim => claim.Type == "security:lockout-level" && claim.Value == "1");
            await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddSeconds(-1));
            await users.ResetAccessFailedCountAsync(user!);
        }

        var successfulLogin = await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = AuditFactory.Password });
        Assert.Equal(HttpStatusCode.Redirect, successfulLogin.StatusCode);
        using var verify = f.Services.CreateScope();
        var verifiedUser = await verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.NotNull(verifiedUser);
        Assert.DoesNotContain(await verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetClaimsAsync(verifiedUser!), claim => claim.Type == "security:lockout-level");
    }

    [Fact]
    public async Task DeveloperCanRetryExhaustedEmailWithoutExposingRecipient()
    {
        var isolated = new AuditFactory { ProcessEmail = false };
        await isolated.InitializeAsync();
        try
        {
        var recipient = $"private-{Guid.NewGuid():N}@example.test";
        int outboxId;
        using (var scope = isolated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = new EmailOutbox
            {
                Recipient = recipient,
                Kind = "Notification",
                Subject = "Prueba de auditoría",
                TextBody = "Contenido de prueba",
                Attempts = 8,
                LastError = "Entrega fallida; revisar proveedor de correo.",
                NextAttemptAt = DateTime.UtcNow
            };
            db.Add(item);
            await db.SaveChangesAsync();
            outboxId = item.Id;
        }

        using var developer = isolated.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await AuditFactory.Post(developer, "/Account/Login", new()
        { ["Email"] = "developer@example.test", ["Password"] = AuditFactory.Password })).StatusCode);
        var listing = await developer.GetAsync("/Developer/EmailQueue");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        var html = await listing.Content.ReadAsStringAsync();
        Assert.Contains("Reintentar envío", html);
        Assert.DoesNotContain(recipient, html);

        using var owner = await isolated.Login("Owner");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/Developer/EmailQueue")).StatusCode);

        var retry = await AuditFactory.Post(developer, "/Developer/EmailQueue/RetryFailed", new() { ["id"] = outboxId.ToString() }, "/Developer/EmailQueue");
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        using var verify = isolated.Services.CreateScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var queued = await dbVerify.Set<EmailOutbox>().SingleAsync(x => x.Id == outboxId);
        Assert.Equal(0, queued.Attempts);
        Assert.Null(queued.LastError);
        Assert.True(await dbVerify.Set<AuditLog>().AnyAsync(x => x.Action == "EmailOutbox.RetryRequested" && x.EntityId == outboxId.ToString()));
        }
        finally
        {
            await ((IAsyncLifetime)isolated).DisposeAsync();
        }
    }

    [Fact]
    public async Task SqlServerDistributedCacheSharesSessionDataAcrossHosts()
    {
        using var firstHost = f.WithWebHostBuilder(builder => builder
            .UseSetting("Session:Provider", "SqlServer")
            .UseSetting("ConnectionStrings:DefaultConnection", f.TestConnection));
        using var secondHost = f.WithWebHostBuilder(builder => builder
            .UseSetting("Session:Provider", "SqlServer")
            .UseSetting("ConnectionStrings:DefaultConnection", f.TestConnection));
        using (var scope = firstHost.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.StartsWith("BisuteriaAudit_", db.Database.GetDbConnection().Database);
            await db.Database.MigrateAsync();
            Assert.Contains("20261015185347_SqlDistributedSessionStore", await db.Database.GetAppliedMigrationsAsync());
            var cacheOptions = firstHost.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Caching.SqlServer.SqlServerCacheOptions>>().Value;
            Assert.Equal(db.Database.GetDbConnection().Database, new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cacheOptions.ConnectionString).InitialCatalog);
        }
        var key = $"bisuteria-audit-{Guid.NewGuid():N}";
        var firstCache = firstHost.Services.GetRequiredService<IDistributedCache>();
        Assert.Contains("SqlServerCache", firstCache.GetType().Name);
        await firstCache.SetStringAsync(key, "carrito-compartido", new DistributedCacheEntryOptions
        { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        var secondCache = secondHost.Services.GetRequiredService<IDistributedCache>();
        Assert.Equal("carrito-compartido", await secondCache.GetStringAsync(key));
        await secondCache.RemoveAsync(key);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current += duration;
    }

    [Fact]
    public async Task RoleMatrixSeparatesDeveloperOwnerAndOperatorAccess()
    {
        using var developer = await f.Login("Developer");
        using var owner = await f.Login("Owner");
        using var operatorUser = await f.Login("Operator");

        Assert.Equal(HttpStatusCode.OK, (await developer.GetAsync("/Developer/Roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/Developer/Roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorUser.GetAsync("/Admin/Settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await operatorUser.GetAsync("/Admin/Orders")).StatusCode);
        var operatorPanel = await operatorUser.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.OK, operatorPanel.StatusCode);
        Assert.DoesNotContain("Configuración</a>", await operatorPanel.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnerCanInviteOperatorButCannotInviteOwnerAndInvitationIsSingleUse()
    {
        using var owner = await f.Login("Owner");
        var forbidden = await AuditFactory.Post(owner, "/StaffInvitation/Create", new()
        {
            ["email"] = $"owner-{Guid.NewGuid():N}@example.test", ["roleName"] = "Owner"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var email = $"operator-{Guid.NewGuid():N}@example.test";
        var created = await AuditFactory.Post(owner, "/StaffInvitation/Create", new()
        {
            ["email"] = email, ["roleName"] = "Operator"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = await db.StaffInvitations.SingleAsync(x => x.Email == email && x.AcceptedAtUtc == null && x.RevokedAtUtc == null);
        var protectedBody = await db.Set<EmailOutbox>().Where(x => x.Kind == "StaffInvitation" && x.Recipient == email).Select(x => x.TextBody).SingleAsync();
        Assert.DoesNotContain("token=", protectedBody);
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("BisuteriasDanns.StaffInvitation.Email.v1");
        var token = Regex.Match(protector.Unprotect(protectedBody), @"token=([^\s]+)").Groups[1].Value;
        Assert.NotEmpty(token);

        using var guest = f.Client();
        var accepted = await AuditFactory.Post(guest, "/StaffInvitation/Accept", new()
        {
            ["Token"] = token, ["Email"] = email, ["RoleName"] = "Operator",
            ["Password"] = "Valid-Operator!123", ["ConfirmPassword"] = "Valid-Operator!123"
        });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains("Cuenta activada", await accepted.Content.ReadAsStringAsync());
        await db.Entry(invitation).ReloadAsync();
        Assert.NotNull(invitation.AcceptedAtUtc);

        using var secondGuest = f.Client();
        var reused = await AuditFactory.Post(secondGuest, "/StaffInvitation/Accept", new()
        {
            ["Token"] = token, ["Email"] = email, ["RoleName"] = "Operator",
            ["Password"] = "Valid-Operator!123", ["ConfirmPassword"] = "Valid-Operator!123"
        });
        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);
        Assert.Contains("Invitación no disponible", await reused.Content.ReadAsStringAsync());
        var newUser = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.NotNull(newUser);
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(newUser!, "Operator"));
    }

    [Fact]
    public async Task CustomerContactIsMaskedAndDeveloperRevealIsAudited()
    {
        using var scope = f.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByEmailAsync("customer@example.test");
        Assert.NotNull(user);
        using var developer = await f.Login("Developer");
        var list = await developer.GetAsync("/Admin/Users");
        var html = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(user!.Email!, html);
        Assert.Contains("***@example.test", html);
        var revealed = await AuditFactory.Post(developer, "/Admin/Users/Reveal", new() { ["id"] = user.Id }, "/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, revealed.StatusCode);
        Assert.Contains(user.Email!, await revealed.Content.ReadAsStringAsync());

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Set<AuditLog>().AnyAsync(x => x.Action == "CustomerData.Revealed" && x.EntityId == user.Id));
        using var owner = await f.Login("Owner");
        var denied = await AuditFactory.Post(owner, "/Admin/Users/Reveal", new() { ["id"] = user.Id });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task PermissionChangeInvalidatesAffectedSessionsAndBlocksDirectUrls()
    {
        using var developer = await f.Login("Developer");
        using var operatorClient = await f.Login("Operator");
        var token = await AuditFactory.Token(developer, "/Developer/Roles");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("role", "Operator"), new("__RequestVerificationToken", token)
        };
        fields.AddRange(StorePermissions.OperatorDefault.Where(x => x != StorePermissions.AdminSearch)
            .Select(permission => new KeyValuePair<string, string>("permissions", permission)));
        var save = await developer.PostAsync("/Developer/Roles/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var staleSession = await operatorClient.GetAsync("/Admin/Orders");
        Assert.Equal(HttpStatusCode.Redirect, staleSession.StatusCode);
        Assert.Contains("/Account/Login", staleSession.Headers.Location!.OriginalString);
        using var refreshed = await f.Login("Operator");
        Assert.Equal(HttpStatusCode.Forbidden, (await refreshed.GetAsync("/Admin/Search?q=piece")).StatusCode);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var current = await db.RolePermissions.Where(x => x.RoleName == StoreRoles.Operator).ToListAsync();
        db.RolePermissions.RemoveRange(current);
        db.RolePermissions.AddRange(StorePermissions.OperatorDefault.Select(permission => new RolePermission
        { RoleName = StoreRoles.Operator, Permission = permission }));
        await db.SaveChangesAsync();
        var operatorUser = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("operator@example.test");
        Assert.NotNull(operatorUser);
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().UpdateSecurityStampAsync(operatorUser!)).Succeeded);
    }

    [Fact]
    public async Task ValidationSummaryDoesNotGenerateInlineStyles()
    {
        using var client = f.Client();
        var html = await client.GetStringAsync("/Account/Login");
        Assert.DoesNotContain("style=\"display:none\"", html);
        Assert.Contains("<li hidden></li>", html);
    }

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

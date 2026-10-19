using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartupDisablesLegacyDefaultAdministrator(bool initiallyActive)
    {
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const string legacyEmail = "admin@bisuteriasdanns.com";
        var legacy = await users.FindByEmailAsync(legacyEmail);
        if (legacy is null)
        {
            legacy = new ApplicationUser { UserName = legacyEmail, Email = legacyEmail, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(legacy, AuditFactory.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(legacy, "Admin")).Succeeded);
        }
        legacy.IsActive = initiallyActive;
        legacy.LockoutEnabled = false;
        legacy.LockoutEnd = null;
        Assert.True((await users.UpdateAsync(legacy)).Succeeded);
        var previousStamp = await users.GetSecurityStampAsync(legacy);

        await DbInitializer.InitializeAsync(db, users, roles, new ConfigurationBuilder().Build());

        var disabled = await users.FindByEmailAsync(legacyEmail);
        Assert.NotNull(disabled);
        Assert.False(disabled.IsActive);
        Assert.True(disabled.LockoutEnabled);
        Assert.Equal(DateTimeOffset.MaxValue, disabled.LockoutEnd);
        Assert.True(await users.IsInRoleAsync(disabled, "Admin"));
        Assert.True(previousStamp != await users.GetSecurityStampAsync(disabled), "El sello de seguridad debe renovarse al bloquear la cuenta.");

        var disabledStamp = await users.GetSecurityStampAsync(disabled);
        await DbInitializer.InitializeAsync(db, users, roles, new ConfigurationBuilder().Build(), applyMigrations: false);
        Assert.True(disabledStamp == await users.GetSecurityStampAsync(disabled), "El segundo arranque debe ser idempotente.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupWithoutBootstrapSecretsCreatesNoAccounts(bool production)
    {
        var databaseName = "BisuteriaAudit_" + Guid.NewGuid().ToString("N");
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(f.TestConnection)
        { InitialCatalog = databaseName }.ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Matches("^BisuteriaAudit_[0-9a-f]{32}$", db.Database.GetDbConnection().Database);
        try
        {
            await db.Database.MigrateAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Task Initialize() => DbInitializer.InitializeAsync(db, users, roles,
                new ConfigurationBuilder().Build(), isProduction: production, applyMigrations: false);
            if (production)
                await Assert.ThrowsAsync<InvalidOperationException>(Initialize);
            else
            {
                await Initialize();
                await Initialize();
            }
            Assert.False(await db.Users.AnyAsync());
        }
        finally
        {
            Assert.Equal(databaseName, db.Database.GetDbConnection().Database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public void VersionedApplicationSettingsContainNoPasswordOrSecretValues()
    {
        var root = FindRepositoryRoot();
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "ls-files", "-z", "--", "**/appsettings*.json", "**/secrets.json" }) start.ArgumentList.Add(argument);
        using var git = Process.Start(start)!;
        var files = git.StandardOutput.ReadToEnd().Split('\0', StringSplitOptions.RemoveEmptyEntries);
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
        Assert.NotEmpty(files);

        var findings = new List<string>();
        foreach (var file in files)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, file)));
            FindSecrets(json.RootElement, file, findings);
        }

        Assert.Empty(findings);
    }

    private static void FindSecrets(JsonElement element, string path, ICollection<string> findings)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}:{property.Name}";
                if (Regex.IsMatch(property.Name, "password|passwd|secret|api.?key|access.?token", RegexOptions.IgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    findings.Add(propertyPath);
                else
                    FindSecrets(property.Value, propertyPath, findings);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var value in element.EnumerateArray())
                FindSecrets(value, $"{path}[{index++}]", findings);
        }
        else if (element.ValueKind == JsonValueKind.String &&
                 path.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase) &&
                 Regex.IsMatch(element.GetString() ?? string.Empty, @"(?i)(Password|Pwd)\s*=\s*[^;\s]+"))
        {
            findings.Add(path);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bisuteria.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio para validar la configuración.");
    }
}

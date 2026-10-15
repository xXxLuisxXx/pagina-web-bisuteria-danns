using System.Text.Json;
using System.Text.RegularExpressions;
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
    [Fact]
    public async Task StartupDisablesLegacyDefaultAdministrator()
    {
        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const string legacyEmail = "admin@bisuteriasdanns.com";
        var legacy = new ApplicationUser
        {
            UserName = legacyEmail,
            Email = legacyEmail,
            EmailConfirmed = true,
            IsActive = true
        };
        Assert.True((await users.CreateAsync(legacy, AuditFactory.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(legacy, "Admin")).Succeeded);

        await DbInitializer.InitializeAsync(db, users, roles, new ConfigurationBuilder().Build());

        var disabled = await users.FindByEmailAsync(legacyEmail);
        Assert.NotNull(disabled);
        Assert.False(disabled.IsActive);
        Assert.True(disabled.LockoutEnabled);
        Assert.Equal(DateTimeOffset.MaxValue, disabled.LockoutEnd);
        Assert.True(await users.IsInRoleAsync(disabled, "Admin"));
    }

    [Fact]
    public void VersionedApplicationSettingsContainNoPasswordOrSecretValues()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "Bisuteria.Web"), "appsettings*.json");
        Assert.NotEmpty(files);

        var findings = new List<string>();
        foreach (var file in files)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            FindSecrets(json.RootElement, Path.GetFileName(file), findings);
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

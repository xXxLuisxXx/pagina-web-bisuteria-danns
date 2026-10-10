using System.Net;
using System.Text.RegularExpressions;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Common;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Validators;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

public sealed class MailCapture : IEmailSender
{
    public string? Url { get; private set; }
    public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default)
    { Url = resetUrl; return Task.CompletedTask; }
}

public sealed class AuditFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseName { get; } = "BisuteriaAudit_" + Guid.NewGuid().ToString("N");
    public const string Password = "Audit-Only!12345";
    public int ProductId { get; private set; }
    public int CategoryId { get; private set; }
    public int HiddenProductId { get; private set; }
    public MailCapture Mail { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Initialize", "false");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Database:Initialize"] = "false",
            ["ConnectionStrings:DefaultConnection"] = $"Server=localhost;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True",
            ["PublicBaseUrl"] = "https://localhost",
            ["Payments:EnabledMethods:0"] = "CashOnDelivery"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer($"Server=localhost;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True", sql => sql.EnableRetryOnFailure(3)));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Mail);
        });
    }
    public async Task InitializeAsync()
    {
        // Todas las escrituras se realizan en esta base temporal, nunca en BisuteriaDb.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(DatabaseName, db.Database.GetConnectionString());
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach(var role in new[]{"Admin","Customer"}) Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var role in new[]{"Admin","Customer"})
        {
            var user = new ApplicationUser { UserName=$"{role.ToLower()}@example.test", Email=$"{role.ToLower()}@example.test", FirstName=role, IsActive=true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        var category = new Category { Name="Pruebas aisladas", Slug="audit", IsActive=true };
        var product = new Product { Name="Pieza de ensayo aislada", Slug="audit-product", Price=20, Stock=100, Category=category, IsActive=true, IsCustomizable=true, IsFeatured=true };
        var hidden = new Product { Name="Oculto", Slug="audit-hidden", Price=30, Stock=5, Category=category, IsActive=false };
        db.AddRange(category, product, hidden);
        db.SiteSettings.AddRange(
            new SiteSetting { Key = "WhatsApp", Value = "+1 202 555 0100" },
            new SiteSetting { Key = "InstagramUrl", Value = "https://www.instagram.com/example/" },
            new SiteSetting { Key = "TikTokUrl", Value = "https://www.tiktok.com/@example" },
            new SiteSetting { Key = "FreeShippingThreshold", Value = "" },
            new SiteSetting { Key = "ShippingCost", Value = "0" });
        await db.SaveChangesAsync();
        ProductId=product.Id; HiddenProductId=hidden.Id; CategoryId=category.Id;
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope=Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetDbConnection().Database == DatabaseName && DatabaseName.StartsWith("BisuteriaAudit_"))
            await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"), AllowAutoRedirect=false });
    public static async Task<string> Token(HttpClient client, string path)
    {
        var response=await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var html=await response.Content.ReadAsStringAsync();
        var match=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Falta token en "+path);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    public static async Task<HttpResponseMessage> Post(HttpClient client,string path, Dictionary<string,string> fields,string tokenPath="/Account/Login")
    {
        if (path == "/Checkout" && !fields.ContainsKey("CheckoutToken"))
        {
            var html = await client.GetStringAsync("/Checkout");
            fields["CheckoutToken"] = Regex.Match(html, "name=\"CheckoutToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        }
        fields["__RequestVerificationToken"]=await Token(client,tokenPath);
        return await client.PostAsync(path,new FormUrlEncodedContent(fields));
    }
    public async Task<HttpClient> Login(string role)
    {
        var client=Client();
        var response=await Post(client,"/Account/Login",new(){["Email"]=$"{role.ToLower()}@example.test",["Password"]=Password});
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        return client;
    }
}

public sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName {get;set;} = "Audit";
    public string EnvironmentName {get;set;} = "Testing";
    public string ContentRootPath {get;set;} = "";
    public string WebRootPath {get;set;} = "";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider {get;set;} = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider {get;set;} = new Microsoft.Extensions.FileProviders.NullFileProvider();
}

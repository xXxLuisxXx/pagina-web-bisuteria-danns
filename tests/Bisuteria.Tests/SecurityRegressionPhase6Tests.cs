using System.Net;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task LoginUsesSamePublicErrorForUnknownAndIncorrectCredentials()
    {
        var knownEmail = $"known-{Guid.NewGuid():N}@example.test";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser
            { UserName = knownEmail, Email = knownEmail, IsActive = true }, AuditFactory.Password)).Succeeded);
        }

        using var client = f.Client();
        var wrongPassword = "Wrong!" + Guid.NewGuid().ToString("N");
        var unknown = await AuditFactory.Post(client, "/Account/Login", new()
        { ["Email"] = $"missing-{Guid.NewGuid():N}@example.test", ["Password"] = wrongPassword }, "/Account/Login");
        var incorrect = await AuditFactory.Post(client, "/Account/Login", new()
        { ["Email"] = knownEmail, ["Password"] = wrongPassword }, "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, incorrect.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos.", System.Net.WebUtility.HtmlDecode(await unknown.Content.ReadAsStringAsync()));
        Assert.Contains("Correo o contraseña incorrectos.", System.Net.WebUtility.HtmlDecode(await incorrect.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task GuestConfirmationDoesNotCacheOrLeakAccessTokenAsReferrer()
    {
        var token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        string orderNumber;
        using (var scope = f.Services.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var checkout = new CheckoutDto
            {
                CustomerName = "Invitado de prueba", CustomerEmail = $"guest-{Guid.NewGuid():N}@example.test",
                CustomerPhone = "+593 999 111 222", AddressLine1 = "Calle de prueba", City = "Manta",
                Province = "Manabí", PaymentMethod = PaymentMethod.CashOnDelivery, DeliveryMethod = DeliveryMethod.Pickup,
                CheckoutToken = Guid.NewGuid(), GuestAccessToken = token
            };
            var order = await orders.CreateAsync(checkout,
                new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
            orderNumber = order.OrderNumber;
        }

        using var client = f.Client();
        var response = await client.GetAsync($"/Checkout/Confirmation?orderNumber={Uri.EscapeDataString(orderNumber)}&accessToken={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public void ProofStorageRejectsTheWebRootItselfAndAnyChildDirectory()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "BisuteriaProofPath_" + Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            var environment = new TestWebEnvironment(contentRoot, webRoot);
            var rootConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PaymentProofs:StoragePath"] = webRoot }).Build();
            var childConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PaymentProofs:StoragePath"] = Path.Combine(webRoot, "private") }).Build();

            Assert.Throws<InvalidOperationException>(() => new PaymentProofStorage(environment, rootConfig));
            Assert.Throws<InvalidOperationException>(() => new PaymentProofStorage(environment, childConfig));
            Assert.IsType<PaymentProofStorage>(new PaymentProofStorage(environment,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["PaymentProofs:StoragePath"] = Path.Combine(contentRoot, "App_Data", "proofs") }).Build()));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private sealed class TestWebEnvironment(string contentRoot, string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Bisuteria.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
    }
}

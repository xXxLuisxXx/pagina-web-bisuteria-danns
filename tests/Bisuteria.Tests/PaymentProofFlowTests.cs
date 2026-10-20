using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Web.Services;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task PrivateTransferProofsAreValidatedLimitedAndReviewedAtomically()
    {
        string orderNumber;
        string accessToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        int orderId;
        int firstProofId;
        using (var scope = f.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var orders = services.GetRequiredService<IOrderService>();
            var checkout = Checkout();
            checkout.CheckoutToken = Guid.NewGuid();
            checkout.GuestAccessToken = accessToken;
            checkout.PaymentMethod = PaymentMethod.BankTransfer;
            var order = await orders.CreateAsync(checkout, new CartDto
            { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
            orderId = order.Id;
            orderNumber = order.OrderNumber;
            Assert.True(await orders.ConfirmShippingAsync(orderId, 0m, "owner-test", StoreRoles.Owner));
        }

        using (var scope = f.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var storage = services.GetRequiredService<IPaymentProofStorage>();
            var proofService = services.GetRequiredService<IPaymentProofService>();
            var jpeg = CreateJpeg();
            for (var i = 0; i < 3; i++)
            {
                var upload = new FormFile(new MemoryStream(jpeg), 0, jpeg.Length, "proof", $"receipt-{i}.jpg")
                { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
                var stored = await storage.SaveAsync(upload);
                var created = await proofService.AddAsync(orderNumber, null, accessToken,
                    stored.StorageKey, stored.ContentType, stored.Length);
                Assert.NotNull(created);
            }

            var fourth = await storage.SaveAsync(new FormFile(new MemoryStream(jpeg), 0, jpeg.Length, "proof", "fourth.jpg")
            { Headers = new HeaderDictionary(), ContentType = "image/jpeg" });
            Assert.Null(await proofService.AddAsync(orderNumber, null, accessToken, fourth.StorageKey, fourth.ContentType, fourth.Length));
            await storage.DeleteAsync(fourth.StorageKey);

            await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(async () =>
                await storage.SaveAsync(new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("not an image")), 0, 12, "proof", "receipt.jpg")
                { Headers = new HeaderDictionary(), ContentType = "image/jpeg" }));

            Assert.False(await proofService.CanAccessOrderAsync(orderNumber, null, accessToken + "x"));
            var listed = await proofService.GetForOrderAsync(orderNumber);
            Assert.Equal(3, listed.Count);
            firstProofId = listed.OrderBy(x => x.CreatedAt).First().Id;
            Assert.True(await proofService.ReviewAsync(firstProofId, approve: false, "El comprobante no muestra el monto.", "owner-test", StoreRoles.Owner));
            Assert.False(await proofService.ReviewAsync(listed.Last().Id, approve: false, " ", "owner-test", StoreRoles.Owner));
        }

        using var client = f.Client();
        var goodPath = $"/Checkout/PaymentProof?id={firstProofId}&orderNumber={Uri.EscapeDataString(orderNumber)}&accessToken={Uri.EscapeDataString(accessToken)}";
        var fileResponse = await client.GetAsync(goodPath);
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        Assert.Equal("no-store", fileResponse.Headers.CacheControl!.ToString());
        var badPath = $"/Checkout/PaymentProof?id={firstProofId}&orderNumber={Uri.EscapeDataString(orderNumber)}&accessToken=incorrecto";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(badPath)).StatusCode);

        var confirmationPath = $"/Checkout/Confirmation?orderNumber={Uri.EscapeDataString(orderNumber)}&accessToken={Uri.EscapeDataString(accessToken)}";
        using (var upload = new MultipartFormDataContent())
        {
            upload.Add(new StringContent(await AuditFactory.Token(client, confirmationPath)), "__RequestVerificationToken");
            upload.Add(new StringContent(orderNumber), "orderNumber");
            upload.Add(new StringContent(accessToken), "accessToken");
            var image = new ByteArrayContent(CreateJpeg());
            image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            upload.Add(image, "proof", "fourth.jpg");
            var response = await client.PostAsync("/Checkout/UploadPaymentProof", upload);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        Assert.Equal(3, Directory.GetFiles(f.ProofStoragePath).Length);

        int pendingProofId;
        using (var scope = f.Services.CreateScope())
        {
            var proof = await scope.ServiceProvider.GetRequiredService<IPaymentProofService>().GetForOrderAsync(orderNumber);
            pendingProofId = proof.First(x => x.Status == PaymentProofStatus.Pending).Id;
        }
        using var owner = await f.Login("Owner");
        var approve = await AuditFactory.Post(owner, "/Admin/Orders/ReviewProof", new()
        { ["id"] = pendingProofId.ToString(), ["approve"] = "true" }, "/Account/Authenticator");
        Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode);

        using var verify = f.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedOrder = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId);
        var savedPayment = await db.Payments.AsNoTracking().SingleAsync(x => x.OrderId == orderId);
        var savedProof = await db.PaymentProofs.AsNoTracking().SingleAsync(x => x.Id == pendingProofId);
        Assert.Equal(PaymentStatus.Paid, savedOrder.PaymentStatus);
        Assert.Equal(savedOrder.Total, savedPayment.Amount);
        Assert.Equal(OrderStatus.Confirmed, savedOrder.Status);
        Assert.Equal(PaymentProofStatus.Approved, savedProof.Status);
        Assert.NotEqual(accessToken, await db.Orders.AsNoTracking().Where(x => x.Id == orderId).Select(x => x.GuestAccessTokenHash).SingleAsync());
        Assert.True(await db.Set<EmailOutbox>().AnyAsync(x => x.Recipient == savedOrder.CustomerEmail && x.Subject == "Revisión de comprobante"));
    }

    [Fact]
    public async Task PaymentProofStorageRejectsActivePdfContent()
    {
        using var scope = f.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IPaymentProofStorage>();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n/JavaScript (app.alert('x'))\n%%EOF".PadRight(600));
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "proof", "receipt.pdf")
        { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(() => storage.SaveAsync(file));
    }

    [Fact]
    public async Task ConcurrentUploadsNeverExceedThreeProofsPerOrder()
    {
        string orderNumber;
        int orderId;
        string token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        using (var scope = f.Services.CreateScope())
        {
            var checkout = Checkout();
            checkout.CheckoutToken = Guid.NewGuid();
            checkout.GuestAccessToken = token;
            checkout.PaymentMethod = PaymentMethod.BankTransfer;
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var order = await orders.CreateAsync(checkout, new CartDto
            { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
            orderId = order.Id;
            orderNumber = order.OrderNumber;
            Assert.True(await orders.ConfirmShippingAsync(order.Id, 0, "owner-test", StoreRoles.Owner));
        }

        async Task<int?> UploadOne()
        {
            using var scope = f.Services.CreateScope();
            var services = scope.ServiceProvider;
            var storage = services.GetRequiredService<IPaymentProofStorage>();
            var proofs = services.GetRequiredService<IPaymentProofService>();
            var image = CreateJpeg();
            var stored = await storage.SaveAsync(new FormFile(new MemoryStream(image), 0, image.Length, "proof", "race.jpg")
            { Headers = new HeaderDictionary(), ContentType = "image/jpeg" });
            var id = await proofs.AddAsync(orderNumber, null, token, stored.StorageKey, stored.ContentType, stored.Length);
            if (id is null) await storage.DeleteAsync(stored.StorageKey);
            return id;
        }

        var ids = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => UploadOne()));
        Assert.Equal(3, ids.Count(x => x.HasValue));
        using var verify = f.Services.CreateScope();
        Assert.Equal(3, await verify.ServiceProvider.GetRequiredService<AppDbContext>().PaymentProofs.CountAsync(x => x.OrderId == orderId));
    }

    [Fact]
    public async Task BankTransferCanOnlyBeEnabledAfterBankDetailsAreFilled()
    {
        using var admin = await f.Login("Admin");
        var rejected = await AuditFactory.Post(admin, "/Admin/Settings/Save", new()
        { ["values[PaymentBankTransferEnabled]"] = "true" }, "/Admin/Settings");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Completa este dato", await rejected.Content.ReadAsStringAsync());

        var saved = await AuditFactory.Post(admin, "/Admin/Settings/Save", new()
        {
            ["values[PaymentBankTransferEnabled]"] = "true",
            ["values[BankName]"] = "Banco de prueba",
            ["values[BankAccountType]"] = "Ahorros",
            ["values[BankAccountNumber]"] = "0000000000",
            ["values[BankAccountHolder]"] = "Titular de prueba"
        }, "/Admin/Settings");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        using var client = f.Client();
        await AuditFactory.Post(client, "/Cart/Add", new() { ["ProductId"] = f.ProductId.ToString(), ["Quantity"] = "1" });
        var checkout = await client.GetStringAsync("/Checkout");
        Assert.Contains("Transferencia bancaria", checkout);
    }

    private static byte[] CreateJpeg()
    {
        using var image = new MagickImage(MagickColors.White, 32, 32);
        image.Format = MagickFormat.Jpeg;
        using var output = new MemoryStream();
        image.Write(output);
        return output.ToArray();
    }
}

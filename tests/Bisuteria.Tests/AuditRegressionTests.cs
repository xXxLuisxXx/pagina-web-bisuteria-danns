using System.Net;
using System.Text.RegularExpressions;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Services;
using Bisuteria.Application.Validators;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task PickupCheckoutDoesNotRequireAddressOverHttp()
    {
        using var client = f.Client();
        await AuditFactory.Post(client, "/Cart/Add", new() { ["ProductId"] = f.ProductId.ToString(), ["Quantity"] = "1" });
        var result = await AuditFactory.Post(client, "/Checkout", new()
        {
            ["CustomerName"] = "Retiro de prueba", ["CustomerEmail"] = "pickup@example.test",
            ["CustomerPhone"] = "2025550100", ["DeliveryMethod"] = "Pickup", ["PaymentMethod"] = "CashOnDelivery",
            ["AddressLine1"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("Confirmation", result.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task TransparentPngBecomesWhiteJpeg()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BisuteriaAuditImages_" + Guid.NewGuid().ToString("N"));
        var service = new Bisuteria.Web.Services.ImageUploadService(new TestEnvironment { WebRootPath = folder, ContentRootPath = folder }, Microsoft.Extensions.Logging.Abstractions.NullLogger<Bisuteria.Web.Services.ImageUploadService>.Instance);
        using var png = new MemoryStream();
        using (var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.Transparent, 4, 4))
            await image.WriteAsync(png, ImageMagick.MagickFormat.Png);
        png.Position = 0;
        var file = new Microsoft.AspNetCore.Http.FormFile(png, 0, png.Length, "image", "transparent.png")
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "image/png"
        };
        var url = await service.SaveOptimizedAsync(file, "products");
        try
        {
            using var result = new ImageMagick.MagickImage(Path.Combine(folder, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            var pixel = result.GetPixels().GetPixel(0, 0).ToColor()!;
            Assert.True(pixel.R > 245 && pixel.G > 245 && pixel.B > 245);
        }
        finally { service.DiscardNewUploads([url]); }
    }

    [Fact]
    public async Task DuplicateSkuIsRejectedWithoutChangingTheOtherProduct()
    {
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductAdminService>();
        var sku = Guid.NewGuid().ToString("N");
        var first = new ProductInputDto { Name = "SKU " + sku, CategoryId = f.CategoryId, Price = 10, Stock = 1, Sku = sku };
        await service.SaveAsync(first, []);
        var second = new ProductInputDto { Name = "Otro " + sku, CategoryId = f.CategoryId, Price = 10, Stock = 1, Sku = sku };
        var error = await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(() => service.SaveAsync(second, []));
        Assert.Contains("SKU", error.Message);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.CountAsync(p => p.Sku == sku));
    }

    [Fact]
    public async Task CancellationReleasesCouponOnceAndSingleUseCanBeEnabled()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var code = Guid.NewGuid().ToString("N");
        db.Coupons.Add(new Coupon { Code = code.ToUpperInvariant(), Value = 10, Type = DiscountType.Percentage, IsActive = true });
        await db.SaveChangesAsync();
        await settings.SetAsync("CouponSingleUsePerCustomer", "true");
        try
        {
            var cart = new CartDto { CouponCode = code, Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] };
            var order = await service.CreateAsync(Checkout(), cart, "coupon-test");
            await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(() => service.CreateAsync(Checkout(), cart, "coupon-test"));
            Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled));
            Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled));
            Assert.Equal(0, (await db.Coupons.AsNoTracking().SingleAsync(c => c.Code == code.ToUpperInvariant())).UsedCount);
            Assert.Equal(code.ToUpperInvariant(), (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).CouponCode);
        }
        finally { await settings.SetAsync("CouponSingleUsePerCustomer", "false"); }
    }

    [Fact]
    public async Task DisabledLoginHasGenericResponseAndNeverCreatesSession()
    {
        var email = $"inactive-{Guid.NewGuid():N}@example.test";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Bisuteria.Infrastructure.Identity.ApplicationUser>>();
            Assert.True((await users.CreateAsync(new() { Email = email, UserName = email, IsActive = false }, AuditFactory.Password)).Succeeded);
        }
        using var client = f.Client();
        var response = await AuditFactory.Post(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = AuditFactory.Password });
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Correo o contraseña incorrectos.", html);
        Assert.DoesNotContain("desactivada", html);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Theory]
    [InlineData(20, 25, true)]
    [InlineData(20, 20, false)]
    [InlineData(20, 15, false)]
    [InlineData(20.001, 25, false)]
    [InlineData(20, 25.001, false)]
    public void PriceValidationUsesSalePrice(decimal price, decimal previous, bool valid)
    {
        var dto = new ProductInputDto { Name = "Ensayo", CategoryId = 1, Price = price, ComparePrice = previous };
        Assert.Equal(valid, new ProductInputValidator().Validate(dto).IsValid);
        Assert.Equal(price, new Product { Price = price, ComparePrice = previous }.EffectivePrice);
    }

    [Fact]
    public async Task SettingsCanChangeContactAndDisableFreeShipping()
    {
        using var client = await f.Login("Admin");
        var result = await AuditFactory.Post(client, "/Admin/Settings/Save", new()
        {
            ["values[WhatsApp]"] = "+1 202 555 0199", ["values[FreeShippingThreshold]"] = "",
            ["values[PrivacyPolicyText]"] = "<script>alert('xss')</script>\nTexto ficticio editable"
        });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        using var scope = f.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        Assert.Equal("+1 202 555 0199", (await settings.GetAllAsync())["WhatsApp"]);
        Assert.Null(await settings.GetFreeShippingThresholdAsync());
        var privacy = await client.GetStringAsync("/Home/Privacy");
        Assert.Contains("&lt;script&gt;", privacy);
        Assert.Contains("Texto ficticio editable", privacy);
        Assert.DoesNotContain("<script>alert('xss')</script>", privacy);
        await settings.SetAsync("WhatsApp", "+1 202 555 0100");
        await settings.SetAsync("PrivacyPolicyText", "");
    }

    [Theory]
    [InlineData(DeliveryMethod.Pickup, true)]
    [InlineData(DeliveryMethod.Shipping, false)]
    [InlineData(DeliveryMethod.LocalDelivery, false)]
    public void AddressRequiredOnlyForDelivery(DeliveryMethod method, bool valid)
    {
        var checkout = Checkout();
        checkout.DeliveryMethod = method;
        checkout.AddressLine1 = "";
        checkout.DeliveryZoneId = null;
        Assert.Equal(valid, new CheckoutValidator().Validate(checkout).IsValid);
    }

    [Fact]
    public void CheckoutRequiresZoneOnlyWhenShipping()
    {
        var checkout = Checkout();
        Assert.True(new CheckoutValidator().Validate(checkout).IsValid);
        checkout.DeliveryZoneId = null;
        Assert.Contains(new CheckoutValidator().Validate(checkout).Errors, error => error.PropertyName == nameof(checkout.DeliveryZoneId));
    }

    [Fact]
    public async Task SalesExcludeCancelledAndUseGuayaquilDay()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var order = await service.CreateAsync(Checkout(), new CartDto { Items = [new CartItemDto { ProductId = f.ProductId, Quantity = 1 }] }, null);
        var entity = await db.Orders.SingleAsync(o => o.Id == order.Id);
        entity.CreatedAt = new DateTime(2030, 1, 2, 4, 59, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var admin = scope.ServiceProvider.GetRequiredService<IAdminService>();
        Assert.Contains(await admin.GetSalesReportAsync(new DateTime(2030, 1, 1), new DateTime(2030, 1, 1)), o => o.OrderNumber == order.OrderNumber);
        Assert.DoesNotContain(await admin.GetSalesReportAsync(new DateTime(2030, 1, 2), new DateTime(2030, 1, 2)), o => o.OrderNumber == order.OrderNumber);
        Assert.DoesNotContain(await admin.GetSalesReportAsync(null, null, payment: PaymentStatus.Paid), o => o.OrderNumber == order.OrderNumber);
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled));
        Assert.DoesNotContain(await admin.GetSalesReportAsync(null, null), o => o.OrderNumber == order.OrderNumber);
    }

    [Fact]
    public async Task ConcurrentHttpCheckoutCreatesOneOrder()
    {
        using var client = f.Client();
        await AuditFactory.Post(client, "/Cart/Add", new() { ["ProductId"] = f.ProductId.ToString(), ["Quantity"] = "1" });
        var html = await client.GetStringAsync("/Checkout");
        var token = Regex.Match(html, "name=\"CheckoutToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var fields = new Dictionary<string, string>
        {
            ["CheckoutToken"] = token, ["__RequestVerificationToken"] = await AuditFactory.Token(client, "/Checkout"),
            ["CustomerName"] = "Ensayo concurrente", ["CustomerEmail"] = "concurrent@example.test",
            ["CustomerPhone"] = "2025550100", ["AddressLine1"] = "Ensayo", ["City"] = "Manta", ["DeliveryZoneId"] = f.DeliveryZoneId.ToString(),
            ["PaymentMethod"] = "CashOnDelivery", ["DeliveryMethod"] = "Shipping"
        };
        var responses = await Task.WhenAll(client.PostAsync("/Checkout", new FormUrlEncodedContent(fields)), client.PostAsync("/Checkout", new FormUrlEncodedContent(fields)));
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest));
        using var scope = f.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.CountAsync(o => o.CheckoutToken == Guid.Parse(token)));
    }
}

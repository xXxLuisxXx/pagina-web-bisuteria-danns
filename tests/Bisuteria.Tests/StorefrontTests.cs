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

public partial class AuditTests : IClassFixture<AuditFactory>
{
    private readonly AuditFactory f;
    public AuditTests(AuditFactory factory) => f=factory;
    [Theory]
    [InlineData("/")][InlineData("/Catalog")][InlineData("/Catalog?sort=price_asc&minPrice=1&maxPrice=100")]
    [InlineData("/Gallery")][InlineData("/Home/About")][InlineData("/Contact")][InlineData("/Customizer")]
    [InlineData("/Account/Login")][InlineData("/Account/Register")][InlineData("/Account/ForgotPassword")][InlineData("/Cart")]
    public async Task PublicPages(string path) => Assert.Equal(HttpStatusCode.OK,(await f.Client().GetAsync(path)).StatusCode);

    [Fact] public async Task InactiveProductsRemainPrivate()
    {
        using var client=f.Client();
        var html=await client.GetStringAsync("/Catalog?OnlyActive=false");
        Assert.DoesNotContain("audit-hidden",html);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Catalog/Details?id={f.HiddenProductId}")).StatusCode);
    }
    [Theory][InlineData("/Admin")][InlineData("/Admin/Products")][InlineData("/Admin/Users")][InlineData("/Admin/Settings")]
    public async Task VisitorCannotAccessAdmin(string path)
    { var result=await f.Client().GetAsync(path);Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);Assert.Contains("/Account/Login",result.Headers.Location!.OriginalString); }

    [Theory][InlineData("/Admin/Products/Edit")][InlineData("/Admin/Products/Delete")][InlineData("/Admin/Settings/Save")][InlineData("/Admin/Inventory/AddStock")]
    public async Task CustomerCannotWriteAdmin(string path)
    {
        using var client=await f.Login("Customer");
        var result=await AuditFactory.Post(client,path,new(){["Id"]=f.ProductId.ToString(),["Name"]="Ataque"});
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        Assert.Contains("AccessDenied",result.Headers.Location!.OriginalString);
    }
    [Fact] public async Task VisitorCannotWriteAdmin()
    {
        using var client=f.Client();
        var result=await AuditFactory.Post(client,"/Admin/Products/Delete",new(){["Id"]=f.ProductId.ToString()});
        Assert.Contains("/Account/Login",result.Headers.Location!.OriginalString);
    }
    [Fact] public async Task CustomerCannotReadAdmin()
    {
        using var client=await f.Login("Customer");
        var result=await client.GetAsync("/Admin");Assert.Contains("AccessDenied",result.Headers.Location!.OriginalString);
    }
    [Fact] public async Task AdminCanEditAndValidationRejectsBadProduct()
    {
        using var client=await f.Login("Admin");
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/Admin/Products/Edit/{f.ProductId}")).StatusCode);
        var result=await AuditFactory.Post(client,"/Admin/Products/Edit",new(){["Id"]="0",["Name"]="",["Price"]="-1",["Stock"]="-9",["CategoryId"]="0"});
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        var html=WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync());Assert.Contains("El precio debe ser mayor",html);
        using var scope=f.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AnyAsync(p=>p.Price<0));
    }
    [Fact] public async Task AdminCreationRoutesWork()
    {
        using var client=await f.Login("Admin");
        foreach(var path in new[]{"/Admin", "/Admin/Categories/Create","/Admin/Blog/Create","/Admin/Coupons/Create","/Admin/Gallery/Edit","/Admin/Inventory"})
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(path)).StatusCode);
    }
    [Fact] public async Task CsrfIsRequired()
    {
        using var client=await f.Login("Admin");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Admin/Products/Delete",new FormUrlEncodedContent(new Dictionary<string,string>{{"id",f.ProductId.ToString()}}))).StatusCode);
    }
    [Fact] public async Task RegisterLoginLogoutAndLocalRedirect()
    {
        using var client=f.Client();var email=$"new-{Guid.NewGuid():N}@example.test";
        var result=await AuditFactory.Post(client,"/Account/Register",new(){["Email"]=email,["FirstName"]="Cliente",["LastName"]="Ensayo",["Password"]=AuditFactory.Password,["ConfirmPassword"]=AuditFactory.Password,["ReturnUrl"]="https://evil.example"});
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);Assert.DoesNotContain("evil",result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/Account/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await AuditFactory.Post(client,"/Account/Logout",new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Account/Profile")).StatusCode);
        result=await AuditFactory.Post(client,"/Account/Login",new(){["Email"]=email,["Password"]=AuditFactory.Password});Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
    }
    [Fact] public async Task PasswordResetUsesConfiguredOriginAndTokenWorks()
    {
        using var client=f.Client();
        var email=$"reset-{Guid.NewGuid():N}@example.test";
        using(var scope=f.Services.CreateScope())
        { var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();Assert.True((await manager.CreateAsync(new ApplicationUser{Email=email,UserName=email,IsActive=true},AuditFactory.Password)).Succeeded); }
        var result=await AuditFactory.Post(client,"/Account/ForgotPassword",new(){["Email"]=email});
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        for (var attempt = 0; attempt < 100 && f.Mail.Url is null; attempt++) await Task.Delay(50);
        Assert.StartsWith("https://localhost/Account/ResetPassword",f.Mail.Url);
        var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(f.Mail.Url!).Query);
        var fields=new Dictionary<string,string>{["Email"]=email,["Token"]=query["token"].ToString(),["Password"]="Changed-Only!12345",["ConfirmPassword"]="Changed-Only!12345"};
        result=await AuditFactory.Post(client,"/Account/ResetPassword",fields);Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        result=await AuditFactory.Post(client,"/Account/ResetPassword",fields);Assert.Equal(HttpStatusCode.OK,result.StatusCode);
    }
    [Fact] public async Task DisabledUserLosesExistingSession()
    {
        var email=$"disabled-{Guid.NewGuid():N}@example.test";
        using(var scope=f.Services.CreateScope())
        { var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();await manager.CreateAsync(new ApplicationUser{Email=email,UserName=email,IsActive=true},AuditFactory.Password); }
        using var client=f.Client();await AuditFactory.Post(client,"/Account/Login",new(){["Email"]=email,["Password"]=AuditFactory.Password});
        using(var scope=f.Services.CreateScope())
        {var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var user=await manager.FindByEmailAsync(email);user!.IsActive=false;await manager.UpdateAsync(user);}
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Account/Profile")).StatusCode);
    }
    [Fact] public async Task CartClearAndQuantityValidation()
    {
        using var client=f.Client();
        await AuditFactory.Post(client,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="-1"});
        Assert.Contains("\"count\":0",await client.GetStringAsync("/Cart/Count"));
        await AuditFactory.Post(client,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="2"});
        Assert.Contains("\"count\":2",await client.GetStringAsync("/Cart/Count"));
        await AuditFactory.Post(client,"/Cart/UpdateQuantity",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="3"});
        Assert.Contains("\"count\":3",await client.GetStringAsync("/Cart/Count"));
        await AuditFactory.Post(client,"/Cart/UpdateQuantity",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="200"});
        Assert.Contains("\"count\":3",await client.GetStringAsync("/Cart/Count"));
        await AuditFactory.Post(client,"/Cart/Clear",new());Assert.Contains("\"count\":0",await client.GetStringAsync("/Cart/Count"));
    }
    [Fact] public async Task CustomizedCartLinesAreIndependent()
    {
        using var client=f.Client();
        await AuditFactory.Post(client,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="1",["Customization.Text"]="A"});
        await AuditFactory.Post(client,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="1",["Customization.Text"]="B"});
        var html=await client.GetStringAsync("/Cart");
        var id=Regex.Match(html,"name=\"CustomizationId\" value=\"(\\d+)\"").Groups[1].Value;Assert.NotEmpty(id);
        await AuditFactory.Post(client,"/Cart/Remove",new(){["ProductId"]=f.ProductId.ToString(),["CustomizationId"]=id});
        Assert.Contains("\"count\":1",await client.GetStringAsync("/Cart/Count"));
    }
    [Fact] public async Task OrderCancellationRestoresStockOnlyOnce()
    {
        using var scope=f.Services.CreateScope();var services=scope.ServiceProvider;
        var orders=services.GetRequiredService<IOrderService>();var db=services.GetRequiredService<AppDbContext>();
        var before=await db.Products.AsNoTracking().Where(p=>p.Id==f.ProductId).Select(p=>p.Stock).SingleAsync();
        var order=await orders.CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=f.ProductId,Name="Ensayo",Quantity=2,UnitPrice=20}]},null);
        Assert.True(await orders.UpdateStatusAsync(order.Id,OrderStatus.Cancelled));
        Assert.True(await orders.UpdateStatusAsync(order.Id,OrderStatus.Cancelled));
        Assert.False(await orders.UpdateStatusAsync(order.Id,OrderStatus.Pending));
        Assert.Equal(before,await db.Products.AsNoTracking().Where(p=>p.Id==f.ProductId).Select(p=>p.Stock).SingleAsync());
    }
    [Fact] public async Task AggregateStockRejectsMultipleVariantsOverStock()
    {
        using var scope=f.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product=new Product{Name="Stock agregado",Slug=Guid.NewGuid().ToString("N"),CategoryId=f.CategoryId,Price=20,Stock=5,IsActive=true};
        db.Add(product);await db.SaveChangesAsync();
        var orders=scope.ServiceProvider.GetRequiredService<IOrderService>();
        var count=await db.Orders.CountAsync();
        var error=await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>orders.CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=product.Id,Quantity=4,UnitPrice=20},new CartItemDto{ProductId=product.Id,Quantity=4,UnitPrice=20}]},null));
        Assert.StartsWith("Stock insuficiente",error.Message);
        Assert.Equal(5,(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==product.Id)).Stock);
        Assert.Equal(count,await db.Orders.CountAsync());
    }
    [Fact] public async Task UnknownOrderIsNotDisclosed()
    { using var client=await f.Login("Customer");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/Order/Details?orderNumber=nonexistent")).StatusCode); }
    [Fact] public async Task SocialLinksAndSingleWhatsApp()
    {
        var html=await f.Client().GetStringAsync("/Contact");Assert.Single(Regex.Matches(html,"class=\"whatsapp-float\""));
        Assert.Contains("wa.me/12025550100",html);Assert.Contains("instagram.com/example/",html);Assert.Contains("tiktok.com/@example",html);Assert.DoesNotContain("newsletter",html,StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task SitemapLinksBindSlug()
    { var xml=await f.Client().GetStringAsync("/sitemap.xml");Assert.Contains("slug=audit-product",xml);Assert.DoesNotContain("audit-hidden",xml); }
    [Fact] public async Task MigrationsMatchModel()
    { using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.False(db.Database.HasPendingModelChanges()); }
    [Theory][InlineData("javascript:alert(1)")][InlineData("//evil.example/image.png")][InlineData("/img/../secret")][InlineData("http://example.test/x.png")]
    public void RejectUnsafeImageUrls(string url) => Assert.False(InputValidation.IsImageUrl(url));

    [Fact] public async Task AdminProductCrudAndImageRemoval()
    {
        using var client=await f.Login("Admin");
        var name="CRUD-"+Guid.NewGuid().ToString("N");
        var fields=new Dictionary<string,string>{["Name"]=name,["Price"]="14.75",["Stock"]="3",["CategoryId"]=f.CategoryId.ToString(),["IsActive"]="true",["newImages"]="/img/placeholder.svg?a=1\n/img/placeholder.svg?a=2"};
        var result=await AuditFactory.Post(client,"/Admin/Products/Edit",fields);Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        int id,imageId;
        using(var scope=f.Services.CreateScope())
        {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var product=await db.Products.Include(x=>x.Images).SingleAsync(x=>x.Name==name);id=product.Id;Assert.Equal(14.75m,product.Price);Assert.Equal(2,product.Images.Count);imageId=product.Images.First().Id;}
        fields["OriginalStock"]="3";fields["Id"]=id.ToString();fields["RemoveImageIds"]=imageId.ToString();fields.Remove("newImages");fields["Price"]="15.25";
        result=await AuditFactory.Post(client,"/Admin/Products/Edit",fields);Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        using(var scope=f.Services.CreateScope())
        {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.False(await db.ProductImages.AnyAsync(x=>x.Id==imageId));Assert.Equal(15.25m,(await db.Products.FindAsync(id))!.Price);}
        result=await AuditFactory.Post(client,"/Admin/Products/Delete",new(){["Id"]=id.ToString()});Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
    }
    [Fact] public async Task CancelledOrderOwnershipIsEnforced()
    {
        string number;
        using(var scope=f.Services.CreateScope())
        {var service=scope.ServiceProvider.GetRequiredService<IOrderService>();var order=await service.CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=f.ProductId,Name="Ensayo",Quantity=1,UnitPrice=20}]},"another-user");number=order.OrderNumber;await service.UpdateStatusAsync(order.Id,OrderStatus.Cancelled);}
        using var client=await f.Login("Customer");
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/Order/Details?orderNumber="+number)).StatusCode);
        var result=await client.GetAsync("/Checkout/Confirmation?orderNumber="+number);Assert.NotEqual(HttpStatusCode.OK,result.StatusCode);
    }
    [Fact] public async Task ConcurrentOrdersCannotOversell()
    {
        int id;
        using(var scope=f.Services.CreateScope())
        {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var product=new Product{Name="Última pieza",Slug=Guid.NewGuid().ToString("N"),CategoryId=f.CategoryId,Price=10,Stock=1,IsActive=true};db.Add(product);await db.SaveChangesAsync();id=product.Id;}
        async Task<bool> Buy()
        {
            using var scope=f.Services.CreateScope();
            try {await scope.ServiceProvider.GetRequiredService<IOrderService>().CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=id,Name="Última pieza",Quantity=1,UnitPrice=10}]},null);return true;}
            catch(InvalidOperationException ex) when (ex.Message.StartsWith("Stock insuficiente")){return false;}
        }
        var results=await Task.WhenAll(Buy(),Buy());Assert.Single(results, x=>x);
        using var verify=f.Services.CreateScope();Assert.Equal(0,(await verify.ServiceProvider.GetRequiredService<AppDbContext>().Products.FindAsync(id))!.Stock);
    }
    [Fact] public async Task ImageUploadChecksContentAndPath()
    {
        var folder=Path.Combine(Path.GetTempPath(),"BisuteriaAuditImages_"+Guid.NewGuid().ToString("N"));
        var env=new TestEnvironment { WebRootPath=folder,ContentRootPath=folder };
        var service=new ImageUploadService(env,Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageUploadService>.Instance);
        using var png=new MemoryStream();
        using(var image=new ImageMagick.MagickImage(ImageMagick.MagickColors.Black, 4, 4)) await image.WriteAsync(png, ImageMagick.MagickFormat.Png);
        png.Position=0;
        var file=new Microsoft.AspNetCore.Http.FormFile(png,0,png.Length,"image","safe.png"){Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="image/png"};
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>service.SaveOptimizedAsync(file,"../escape"));
        var url=await service.SaveOptimizedAsync(file,"products");Assert.EndsWith(".jpg",url);
        using var text=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>"));
        var fake=new Microsoft.AspNetCore.Http.FormFile(text,0,text.Length,"image","fake.png"){Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="image/png"};
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>service.SaveOptimizedAsync(fake,"products"));
        var large=new Microsoft.AspNetCore.Http.FormFile(text,0,9*1024*1024,"image","big.png"){Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="image/png"};
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>service.SaveOptimizedAsync(large,"products"));
        Directory.Delete(folder,true); // Solo el directorio temporal exclusivo de esta prueba.
    }
    [Fact] public async Task ExportRenderedPagesForResponsiveChecks()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !File.Exists(Path.Combine(root.FullName,"Bisuteria.slnx"))) root=root.Parent;
        var output=Path.Combine(root!.FullName,"audit","rendered");Directory.CreateDirectory(output);
        using var visitor=f.Client();using var admin=await f.Login("Admin");
        foreach(var page in new[]{("home","/"),("catalog","/Catalog"),("product","/Catalog/Details?id="+f.ProductId),("contact","/Contact"),("login","/Account/Login"),("register","/Account/Register"),("forgot","/Account/ForgotPassword"),("customizer","/Customizer?productId="+f.ProductId)})
        {var response=await visitor.GetAsync(page.Item2);Assert.Equal(HttpStatusCode.OK,response.StatusCode);await File.WriteAllTextAsync(Path.Combine(output,page.Item1+".html"),await response.Content.ReadAsStringAsync());}
        await AuditFactory.Post(visitor,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="1"});
        foreach(var page in new[]{("cart","/Cart"),("checkout","/Checkout")}) await File.WriteAllTextAsync(Path.Combine(output,page.Item1+".html"),await visitor.GetStringAsync(page.Item2));
        foreach(var page in new[]{("profile","/Account/Profile"),("admin","/Admin"),("admin-products","/Admin/Products"),("admin-form","/Admin/Products/Edit/"+f.ProductId)}) await File.WriteAllTextAsync(Path.Combine(output,page.Item1+".html"),await admin.GetStringAsync(page.Item2));
    }
    private static CheckoutDto Checkout()=>new(){CustomerName="Cliente aislado",CustomerEmail="test@example.test",CustomerPhone="0982599801",AddressLine1="Dirección de ensayo",City="Ciudad",Province="Provincia",PaymentMethod=PaymentMethod.CashOnDelivery,DeliveryMethod=DeliveryMethod.Shipping};
}

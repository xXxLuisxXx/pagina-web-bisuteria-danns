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



public partial class AuditTests
{
    [Fact]
    public async Task AdminPagesAndExportsRequireAdminForEveryModule()
    {
        using var admin=await f.Login("Admin");using var customer=await f.Login("Customer");using var guest=f.Client();
        foreach(var path in new[]{"/Admin/Categories","/Admin/Orders","/Admin/Users","/Admin/Reviews","/Admin/Messages","/Admin/Coupons","/Admin/Blog","/Admin/Gallery","/Admin/Inventory","/Admin/Reports","/Admin/Reports/Sales","/Admin/Reports/Products","/Admin/Reports/ExportSales","/Admin/Reports/ExportProducts","/Admin/Settings"})
        {
            Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync(path)).StatusCode);
            var denied=await customer.GetAsync(path);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
            Assert.Contains("/Account/Login",(await guest.GetAsync(path)).Headers.Location!.OriginalString);
        }
    }

    [Fact]
    public async Task CategoryBlogAndGalleryCrudPersistAndRespectVisibility()
    {
        using var scope=f.Services.CreateScope();var sp=scope.ServiceProvider;
        var products=sp.GetRequiredService<IProductAdminService>();
        var category=new CategoryInputDto{Name="Categoría-"+Guid.NewGuid().ToString("N"),IsActive=false};
        category.Id=await products.SaveCategoryAsync(category);
        Assert.False((await sp.GetRequiredService<ICatalogService>().GetCategoriesAsync(false)).Single(c=>c.Id==category.Id).IsActive);
        category.IsActive=true;await products.SaveCategoryAsync(category);Assert.True(await products.DeleteCategoryAsync(category.Id));
        Assert.False(await products.DeleteCategoryAsync(f.CategoryId));
        var blog=sp.GetRequiredService<IBlogService>();
        var post=new Bisuteria.Application.DTOs.Blog.BlogPostInputDto{Title="Publicación-"+Guid.NewGuid().ToString("N"),Excerpt="Resumen de prueba",Content="Contenido de prueba",IsPublished=false};
        post.Id=await blog.SaveAsync(post);Assert.DoesNotContain(await blog.GetPublishedAsync(50),b=>b.Id==post.Id);
        post.IsPublished=true;await blog.SaveAsync(post);Assert.Contains(await blog.GetPublishedAsync(50),b=>b.Id==post.Id);Assert.True(await blog.DeleteAsync(post.Id));
        var gallery=sp.GetRequiredService<IGalleryAdminService>();
        var image=new GalleryImageDto{ImageUrl="/img/placeholder.svg",Title="Galería de ensayo"};
        image.Id=await gallery.CreateAsync(image);image.Title="Título actualizado";await gallery.UpdateAsync(image);
        Assert.Equal(image.Title,(await gallery.GetByIdAsync(image.Id))!.Title);
        await gallery.DeleteAsync(image.Id);Assert.Null(await gallery.GetByIdAsync(image.Id));
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>gallery.CreateAsync(new GalleryImageDto{ImageUrl="javascript:alert(1)"}));
    }

    [Fact]
    public async Task CouponLimitsAreAtomicAndInvalidCouponDoesNotConsumeStock()
    {
        var code="TEST"+Guid.NewGuid().ToString("N");
        using(var scope=f.Services.CreateScope())
        {await scope.ServiceProvider.GetRequiredService<ICouponAdminService>().SaveAsync(new Bisuteria.Application.DTOs.Admin.CouponInputDto{Code=code,Value=10,MaxUses=1,IsActive=true});}
        async Task<bool> Buy()
        {
            using var scope=f.Services.CreateScope();
            try {await scope.ServiceProvider.GetRequiredService<IOrderService>().CreateAsync(Checkout(),new CartDto{CouponCode=code,Items=[new CartItemDto{ProductId=f.ProductId,Quantity=1}]},null);return true;}
            catch(InvalidOperationException ex) when(ex.Message.Contains("límite de usos")){return false;}
        }
        var result=await Task.WhenAll(Buy(),Buy());Assert.Single(result,x=>x);
        using var verify=f.Services.CreateScope();var db=verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1,(await db.Coupons.SingleAsync(c=>c.Code==code)).UsedCount);
        var stock=(await db.Products.FindAsync(f.ProductId))!.Stock;
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>verify.ServiceProvider.GetRequiredService<IOrderService>().CreateAsync(Checkout(),new CartDto{CouponCode="NOEXISTE",Items=[new CartItemDto{ProductId=f.ProductId,Quantity=1}]},null));
        Assert.Equal(stock,(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==f.ProductId)).Stock);
    }

    [Fact]
    public async Task ContactPersistsMessageAndAdminCanReadAndDeleteIt()
    {
        using var client=f.Client();var subject="Contacto-"+Guid.NewGuid().ToString("N");
        var result=await AuditFactory.Post(client,"/Contact",new(){["Name"]="Visitante",["Email"]="contact@example.test",["Subject"]=subject,["Message"]="Mensaje válido de prueba de contacto"});
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        using var scope=f.Services.CreateScope();var service=scope.ServiceProvider.GetRequiredService<IContactService>();
        var message=(await service.GetMessagesAsync()).Single(m=>m.Subject==subject);
        Assert.False(message.IsRead);await service.MarkAsReadAsync(message.Id);
        Assert.True((await service.GetMessagesAsync()).Single(m=>m.Id==message.Id).IsRead);
        await service.DeleteAsync(message.Id);Assert.DoesNotContain(await service.GetMessagesAsync(),m=>m.Id==message.Id);
    }

    [Fact]
    public async Task ProductUploadFailureRemovesOnlyNewFiles()
    {
        var folder=Path.Combine(Path.GetTempPath(),"BisuteriaAuditImages_"+Guid.NewGuid().ToString("N"));
        var service=new ImageUploadService(new TestEnvironment{WebRootPath=folder,ContentRootPath=folder},Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageUploadService>.Instance);
        using var png=new MemoryStream();using(var image=new ImageMagick.MagickImage(ImageMagick.MagickColors.Black, 4, 4))await image.WriteAsync(png, ImageMagick.MagickFormat.Png);
        png.Position=0;var good=new Microsoft.AspNetCore.Http.FormFile(png,0,png.Length,"image","safe.png"){Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="image/png"};
        using var badStream=new MemoryStream(new byte[]{1,2,3});var bad=new Microsoft.AspNetCore.Http.FormFile(badStream,0,3,"image","invalid.exe");
        await Assert.ThrowsAsync<Bisuteria.Application.Services.BusinessRuleException>(()=>service.SaveManyAsync([good,bad],"products"));
        Assert.Empty(Directory.GetFiles(folder,"*",SearchOption.AllDirectories));
        Directory.Delete(folder,true);
    }
}

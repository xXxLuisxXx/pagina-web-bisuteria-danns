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
    public async Task OrderUsesCurrentPricesShippingAndProductNames()
    {
        using var scope=f.Services.CreateScope();var sp=scope.ServiceProvider;
        var settings=sp.GetRequiredService<ISettingService>();
        await settings.SetAsync("ShippingCost","7.50");
        var orders=sp.GetRequiredService<IOrderService>();
        var order=await orders.CreateAsync(Checkout(),new CartDto {ShippingCost=0,Items=[new CartItemDto {ProductId=f.ProductId,Name="Precio falsificado",Quantity=2,UnitPrice=0.01m}]},null);
        Assert.Equal(40m,order.Subtotal);Assert.Equal(7.50m,order.ShippingCost);Assert.Equal(47.50m,order.Total);
        Assert.Equal("Pieza de ensayo aislada",order.Items.Single().ProductName);
        await orders.UpdateStatusAsync(order.Id,OrderStatus.Cancelled);
        await settings.SetAsync("ShippingCost","0");
    }

    [Fact]
    public async Task CustomizationPriceIsRecalculatedAndForeignCustomizationRejected()
    {
        using var scope=f.Services.CreateScope();var sp=scope.ServiceProvider;
        var custom=sp.GetRequiredService<ICustomizationService>();
        var id=await custom.SaveAsync(new CustomizationInputDto {ProductId=f.ProductId,Text="Ana",GiftWrap=true,PriceOverride=0.01m});
        var orders=sp.GetRequiredService<IOrderService>();
        var order=await orders.CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=f.ProductId,Quantity=1,UnitPrice=1,CustomizationId=id}]},null);
        Assert.Equal(27m,order.Items.Single().UnitPrice);
        await orders.UpdateStatusAsync(order.Id,OrderStatus.Cancelled);
        var wrong=await custom.SaveAsync(new CustomizationInputDto {ProductId=f.HiddenProductId,Text="Ajena"});
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>orders.CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=f.ProductId,Quantity=1,CustomizationId=wrong}]},null));
        Assert.Contains("personalización",error.Message);
    }

    [Fact]
    public async Task InactiveCategoryBlocksCatalogAndCheckout()
    {
        using var scope=f.Services.CreateScope();var sp=scope.ServiceProvider;var db=sp.GetRequiredService<AppDbContext>();
        var product=new Product{Name="Categoría privada",Slug=Guid.NewGuid().ToString("N"),Price=10,Stock=5,IsActive=true,Category=new Category{Name="Inactiva",Slug=Guid.NewGuid().ToString("N"),IsActive=false}};
        db.Add(product);await db.SaveChangesAsync();
        Assert.Null(await sp.GetRequiredService<ICatalogService>().GetProductAsync(product.Id));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>sp.GetRequiredService<IOrderService>().CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=product.Id,Quantity=1,UnitPrice=10}]},null));
        Assert.Contains("no está a la venta",error.Message);
    }

    [Fact]
    public async Task StaleProductEditCannotRestoreSoldStock()
    {
        using var scope=f.Services.CreateScope();var sp=scope.ServiceProvider;
        var products=sp.GetRequiredService<IProductAdminService>();
        var id=await products.SaveAsync(new ProductInputDto{Name="Concurrencia-"+Guid.NewGuid().ToString("N"),Price=10,Stock=5,CategoryId=f.CategoryId},[]);
        var stale=(await products.GetForEditAsync(id))!;
        var order=await sp.GetRequiredService<IOrderService>().CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=id,Quantity=1}]},null);
        stale.Name="Edición desactualizada";
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>products.SaveAsync(stale,[]));
        Assert.Contains("inventario cambió",error.Message);
        var current=(await products.GetForEditAsync(id))!;Assert.Equal(4,current.Stock);
        current.Stock=7;await products.SaveAsync(current,[]);
        var db=sp.GetRequiredService<AppDbContext>();
        var movements=await db.StockMovements.Where(m=>m.ProductId==id).OrderBy(m=>m.Id).ToListAsync();
        Assert.Equal(new[]{5,4,7},movements.Select(m=>m.StockAfter));
    }

    [Fact]
    public async Task NotesSaveWithoutStatusChangeAndPaymentsStaySynchronized()
    {
        using var scope=f.Services.CreateScope();var sp=scope.ServiceProvider;var service=sp.GetRequiredService<IOrderService>();
        var order=await service.CreateAsync(Checkout(),new CartDto{Items=[new CartItemDto{ProductId=f.ProductId,Quantity=1}]},null);
        Assert.True(await service.UpdateStatusAsync(order.Id,OrderStatus.Pending,"Nota privada"));
        Assert.Equal("Nota privada",await service.GetAdminNotesAsync(order.Id));
        Assert.True(await service.UpdatePaymentStatusAsync(order.Id,PaymentStatus.Paid));
        var db=sp.GetRequiredService<AppDbContext>();
        Assert.All(await db.Payments.Where(p=>p.OrderId==order.Id).ToListAsync(),p=>Assert.Equal(PaymentStatus.Paid,p.Status));
        Assert.False(await service.UpdateStatusAsync(order.Id,OrderStatus.Pending,new string('x',2001)));
        await service.UpdateStatusAsync(order.Id,OrderStatus.Cancelled);
    }

    [Fact]
    public async Task GuestCheckoutPersistsOrderClearsCartAndRestrictsConfirmation()
    {
        using var client=f.Client();
        await AuditFactory.Post(client,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="1"});
        var fields=new Dictionary<string,string>{["CustomerName"]="Compra HTTP",["CustomerEmail"]="http@example.test",["CustomerPhone"]="0982599801",["AddressLine1"]="Dirección de prueba",["AddressLine2"]="Departamento 4B",["PostalCode"]="130802",["City"]="Manta",["Province"]="Manabí",["PaymentMethod"]="CashOnDelivery",["DeliveryMethod"]="Shipping"};
        var result=await AuditFactory.Post(client,"/Checkout",fields);
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);Assert.Contains("Confirmation",result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(result.Headers.Location)).StatusCode);
        var confirmation=await client.GetStringAsync(result.Headers.Location);
        Assert.Contains("Departamento 4B",confirmation);Assert.Contains("130802",confirmation);
        Assert.NotEqual(HttpStatusCode.OK,(await f.Client().GetAsync(result.Headers.Location)).StatusCode);
        Assert.Contains("\"count\":0",await client.GetStringAsync("/Cart/Count"));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order=await db.Orders.Include(o=>o.Items).SingleAsync(o=>o.CustomerEmail=="http@example.test");
        Assert.Single(order.Items);Assert.Equal(20m,order.Subtotal);
        using var admin=await f.Login("Admin");
        var detail=await admin.GetStringAsync("/Admin/Orders/Details?orderNumber="+order.OrderNumber);
        Assert.Contains("Departamento 4B",detail);Assert.Contains("130802",detail);
        var dto=await scope.ServiceProvider.GetRequiredService<IOrderService>().GetByNumberAsync(order.OrderNumber);
        Assert.Equal("Departamento 4B",dto!.AddressLine2);Assert.Equal("130802",dto.PostalCode);
    }

    [Fact]
    public async Task UnconfiguredCardPaymentCannotCreateOrder()
    {
        using var client=f.Client();
        await AuditFactory.Post(client,"/Cart/Add",new(){["ProductId"]=f.ProductId.ToString(),["Quantity"]="1"});
        var result=await AuditFactory.Post(client,"/Checkout",new(){["CustomerName"]="Pago rechazado",["CustomerEmail"]="card@example.test",["CustomerPhone"]="0982599801",["AddressLine1"]="Dirección de prueba",["City"]="Manta",["Province"]="Manabí",["PaymentMethod"]="Card",["DeliveryMethod"]="Shipping"});
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        Assert.Contains("método de pago habilitado",WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync()));
        using var scope=f.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AnyAsync(o=>o.CustomerEmail=="card@example.test"));
    }

    [Fact]
    public async Task ConcurrentReviewsAllowOnlyOnePerUserAndProduct()
    {
        var user="review-"+Guid.NewGuid().ToString("N");
        async Task<bool> Send()
        {using var scope=f.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<IReviewService>().CreateAsync(new CreateReviewDto{ProductId=f.ProductId,Rating=5,Comment="Reseña válida de concurrencia"},user,"Cliente");}
        var results=await Task.WhenAll(Send(),Send());Assert.Single(results,r=>r);
        using var verify=f.Services.CreateScope();Assert.Equal(1,await verify.ServiceProvider.GetRequiredService<AppDbContext>().Reviews.CountAsync(r=>r.UserId==user));
    }

    [Fact]
    public async Task RegistrationCannotAssignAdminRole()
    {
        using var client=f.Client();var email="role-"+Guid.NewGuid().ToString("N")+"@example.test";
        var result=await AuditFactory.Post(client,"/Account/Register",new(){["Email"]=email,["FirstName"]="Cliente",["LastName"]="Seguro",["Password"]=AuditFactory.Password,["ConfirmPassword"]=AuditFactory.Password,["Role"]="Admin",["IsAdmin"]="true"});
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
        using var scope=f.Services.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var user=(await users.FindByEmailAsync(email))!;
        Assert.False(await users.IsInRoleAsync(user,"Admin"));Assert.True(await users.IsInRoleAsync(user,"Customer"));
    }

    [Fact]
    public async Task UnknownRouteReturnsSpanishErrorWithoutStackTrace()
    {
        var result=await f.Client().GetAsync("/ruta-inexistente-de-ensayo");Assert.Equal(HttpStatusCode.NotFound,result.StatusCode);
        var html=WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync());Assert.Contains("encontr",html,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace",html);Assert.DoesNotContain("DeveloperExceptionPage",html);
    }
}

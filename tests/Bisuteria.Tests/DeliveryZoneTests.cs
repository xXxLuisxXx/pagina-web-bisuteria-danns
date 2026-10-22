using System.Net;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ShippingZonesAreGroupedVisibleToOwnerAndProtectedFromOperator()
    {
        using var owner = await f.Login("Owner");
        var page = await owner.GetAsync("/Admin/ShippingZones");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Zonas y tarifas", html);
        Assert.Contains("data-sidebar-group=\"envios\"", html);
        Assert.Contains("aria-expanded=\"true\"", html);
        Assert.Contains("href=\"/Admin/Orders?status=Pending\"", html);

        using var operatorClient = await f.Login("Operator");
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/Admin/ShippingZones")).StatusCode);
        var operatorPage = await operatorClient.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.OK, operatorPage.StatusCode);
        Assert.DoesNotContain("Zonas y tarifas", await operatorPage.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnerCanCreateAuditedShippingZoneAndAntiforgeryIsRequired()
    {
        using var owner = await f.Login("Owner");
        var missingToken = await owner.PostAsync("/Admin/ShippingZones/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["City"] = "Manta", ["Sector"] = "Zona sin antiforgery", ["Cost"] = "3.50"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        var sector = "Zona aislada " + Guid.NewGuid().ToString("N");
        var response = await AuditFactory.Post(owner, "/Admin/ShippingZones/Edit", new()
        {
            ["City"] = "Manta", ["Sector"] = sector, ["Cost"] = "3.50",
            ["IsFree"] = "false", ["RequiresConfirmation"] = "false", ["IsActive"] = "true", ["SortOrder"] = "4"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var zone = await db.DeliveryZones.SingleAsync(item => item.Sector == sector);
        Assert.Equal("Manabí", zone.Province);
        Assert.Equal(3.50m, zone.Cost);
        Assert.True(zone.IsActive);
        Assert.True(await db.Set<AuditLog>().AnyAsync(item => item.Action == "ShippingZone.Created" && item.EntityType == "DeliveryZone"));
    }
}

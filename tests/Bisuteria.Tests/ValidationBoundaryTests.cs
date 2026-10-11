using System.Net;
using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Blog;
using Bisuteria.Application.DTOs.Common;
using Bisuteria.Application.Validators;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

public partial class AuditTests
{
    [Theory]
    [InlineData("Blog/Delete")][InlineData("Products/Delete")][InlineData("Reviews/Approve")][InlineData("Coupons/ToggleActive")]
    public async Task MissingAdministrativeResourceReturnsNotFound(string action)
    {
        using var client = await f.Login("Admin");
        Assert.Equal(HttpStatusCode.NotFound, (await AuditFactory.Post(client, "/Admin/" + action, new() { ["id"] = int.MaxValue.ToString() })).StatusCode);
    }

    [Fact]
    public async Task ShippingSettingsRejectFractionalCentsWithoutSaving()
    {
        using var scope = f.Services.CreateScope();
        var settings = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Bisuteria.Application.Interfaces.ISettingService>(scope.ServiceProvider);
        var before = await settings.GetAsync("ShippingCost");
        using var client = await f.Login("Admin");
        await AuditFactory.Post(client, "/Admin/Settings/Save", new() { ["values[ShippingCost]"] = "1.001" });
        Assert.Equal(before, await settings.GetAsync("ShippingCost"));
    }

    [Theory]
    [InlineData("/Catalog?page=2147483647")]
    [InlineData("/Catalog?sort=invalid")]
    [InlineData("/Catalog?minPrice=20&maxPrice=10")]
    [InlineData("/Blog?page=abc")]
    public async Task InvalidQueryIsRejected(string path)
    {
        using var client = f.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task InvalidReportDatesAreRejected()
    {
        using var client = await f.Login("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Admin/Reports/Sales?from=2026-10-10&to=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Admin/Blog/Edit/-1")).StatusCode);
    }

    [Theory]
    [InlineData("1.01", true)] [InlineData("1.001", false)] [InlineData("-1", false)] [InlineData("101", false)]
    public void CouponPrecisionAndRange(string amount, bool valid)
    {
        var dto = new CouponInputDto { Code = "TEST", Value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture) };
        Assert.Equal(valid, new CouponInputValidator().Validate(dto).IsValid);
    }

    [Theory]
    [InlineData(300, 20000, true)] [InlineData(301, 20000, false)] [InlineData(300, 20001, false)]
    public void BlogTextBoundaries(int tags, int content, bool valid)
        => Assert.Equal(valid, new BlogPostValidator().Validate(new BlogPostInputDto { Title = "Título", Excerpt = "Resumen", Tags = new string('a', tags), Content = new string('a', content) }).IsValid);

    [Theory]
    [InlineData("relevance")] [InlineData("price_asc")] [InlineData("price_desc")] [InlineData("bestsellers")] [InlineData("name_asc")] [InlineData("newest")]
    public void SupportedCatalogSortIsAccepted(string sort) => Assert.True(new ProductQueryValidator().Validate(new ProductQuery { Sort = sort }).IsValid);

    [Fact]
    public async Task BlogFormPublishesClientValidationRules()
    {
        using var client = await f.Login("Admin");
        var html = await client.GetStringAsync("/Admin/Blog/Create");
        Assert.Contains("data-val-length-max=\"220\"", html);
        Assert.Contains("data-val-required=", html);
    }
}

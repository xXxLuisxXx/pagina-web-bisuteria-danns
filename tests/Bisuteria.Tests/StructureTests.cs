using System.Net;
using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Services;
using Bisuteria.Application.DTOs.Cart;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task HealthChecksTemporaryDatabase()
    {
        using var client = f.Client();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task CartPreparationIgnoresBrowserPrice()
    {
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CartPreparationService>();
        var input = new CustomizationInputDto { Text = "Test", PriceOverride = 0.01m };
        var id = await service.PrepareAsync(f.ProductId, 1, 0, input, "isolated-test");
        var saved = await scope.ServiceProvider.GetRequiredService<Bisuteria.Infrastructure.Data.AppDbContext>().Set<Bisuteria.Domain.Entities.Customization>().FindAsync(id!.Value);
        Assert.Equal(24m, saved!.PriceOverride);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.PrepareAsync(f.ProductId, 50, 1, null, "isolated-test"));
    }
    [Fact]
    public async Task UsersHaveStablePaginationAndRoles()
    {
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminService>();
        var first = await service.GetUsersAsync(page: 1, pageSize: 1);
        var second = await service.GetUsersAsync(page: 2, pageSize: 1);
        Assert.Single(first); Assert.Single(second);
        Assert.NotEqual(first[0].Id, second[0].Id);
        var selected = await service.GetUsersAsync(userId: first[0].Id);
        Assert.Equal(first[0].Id, Assert.Single(selected).Id);
    }

    [Fact]
    public async Task BusinessAccountFilterRunsBeforePaginationAndExcludesPrivilegedRoles()
    {
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminService>();
        var page = await service.GetUsersAsync(page: 1, pageSize: 100, businessAccountsOnly: true);
        Assert.NotEmpty(page);
        Assert.All(page, user =>
        {
            Assert.Single(user.Roles);
            Assert.Contains(user.Roles[0], new[] { "Customer", "Operator" });
        });
        Assert.DoesNotContain(page, user => user.Roles.Contains("Owner") || user.Roles.Contains("Developer"));
    }
}

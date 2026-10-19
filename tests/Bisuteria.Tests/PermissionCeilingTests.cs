using System.Net;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Bisuteria.Application.Interfaces;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ConcurrentDeactivationPreservesOneDeveloper()
    {
        await using var isolated = new AuditFactory();
        await isolated.InitializeAsync();
        string firstId;
        string secondId;
        using (var scope = isolated.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            firstId = (await users.FindByEmailAsync("developer@example.test"))!.Id;
            var email = $"second-{Guid.NewGuid():N}@example.test";
            var second = new ApplicationUser { UserName = email, Email = email, IsActive = true };
            Assert.True((await users.CreateAsync(second, AuditFactory.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(second, StoreRoles.Developer)).Succeeded);
            secondId = second.Id;
        }
        async Task<bool> Disable(string id)
        {
            using var scope = isolated.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAdminService>().SetUserActiveAsync(id, false);
        }
        var results = await Task.WhenAll(Disable(firstId), Disable(secondId));
        Assert.Single(results, success => success);
        using var verify = isolated.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(user => user.IsActive && (user.Id == firstId || user.Id == secondId)));
    }

    [Theory]
    [InlineData("Owner", "Roles.EditMatrix")]
    [InlineData("Owner", "Security.Configure")]
    [InlineData("Operator", "Users.ManageOwner")]
    [InlineData("Operator", "Payments.Update")]
    public async Task MatrixCannotElevateCommercialRoles(string role, string permission)
    {
        using var developer = await f.Login("Developer");
        var response = await AuditFactory.Post(developer, "/Developer/Roles/Save",
            new() { ["role"] = role, ["permissions"] = permission }, "/Developer/Roles");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationRejectsTechnicalGrantAccidentallyStoredForOwner()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grant = new RolePermission { RoleName = StoreRoles.Owner, Permission = StorePermissions.RolesViewMatrix };
        db.RolePermissions.Add(grant);
        await db.SaveChangesAsync();
        try
        {
            using var owner = await f.Login("Owner");
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/Developer/Roles")).StatusCode);
        }
        finally { db.RolePermissions.Remove(grant); await db.SaveChangesAsync(); }
    }
}

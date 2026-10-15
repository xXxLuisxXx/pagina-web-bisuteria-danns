using System.Security.Claims;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Security;

public sealed record StorePermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class StorePermissionHandler(AppDbContext db) : AuthorizationHandler<StorePermissionRequirement>
{
    private string? _loadedUserId;
    private HashSet<string>? _permissions;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, StorePermissionRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return;

        if (_loadedUserId != userId)
        {
            _loadedUserId = userId;
            _permissions = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join grant in db.RolePermissions.AsNoTracking() on role.Name equals grant.RoleName
            where userRole.UserId == userId
            select grant.Permission).ToHashSetAsync();
        }

        if (_permissions?.Contains(requirement.Permission) == true) context.Succeed(requirement);
    }
}

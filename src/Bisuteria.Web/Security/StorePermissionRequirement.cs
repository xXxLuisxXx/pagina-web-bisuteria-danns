using System.Security.Claims;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Domain.Security;
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
            var grants = await (
            from userRole in db.UserRoles.AsNoTracking()
            join user in db.Users.AsNoTracking() on userRole.UserId equals user.Id
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join grant in db.RolePermissions.AsNoTracking() on role.Name equals grant.RoleName
            where userRole.UserId == userId && user.IsActive
            select new { Role = role.Name!, grant.Permission }).ToListAsync();
            _permissions = grants.Where(grant =>
                StorePermissions.CanAssignTo(grant.Role, grant.Permission) ||
                // El rol legado solo conserva concesiones explícitas; no se convierte a Developer.
                grant.Role == StoreRoles.LegacyAdmin && StorePermissions.All.Contains(grant.Permission))
                .Select(grant => grant.Permission).ToHashSet(StringComparer.Ordinal);
        }

        if (_permissions?.Contains(requirement.Permission) == true) context.Succeed(requirement);
    }
}

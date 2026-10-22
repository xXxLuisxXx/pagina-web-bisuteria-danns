using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Developer.Controllers;

[Area("Developer"), Authorize(Policy = StorePermissions.RolesViewMatrix)]
public sealed class PermissionsController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? userId = null, CancellationToken ct = default)
    {
        if (page is < 1 or > 10000 || userId?.Length > 450) return BadRequest("Filtro de cuentas no válido.");
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(user => user.Id == userId);
        var accounts = await query.OrderBy(user => user.Id).Skip((page - 1) * 25).Take(26)
            .Select(user => new { user.Id, user.IsActive }).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(userId) && accounts.Count == 0) return NotFound();
        var ids = accounts.Take(25).Select(user => user.Id).ToArray();
        var assignments = await (from link in db.UserRoles.AsNoTracking()
                                 join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id
                                 where ids.Contains(link.UserId)
                                 select new { link.UserId, Role = role.Name! }).ToListAsync(ct);
        var grants = await db.RolePermissions.AsNoTracking().Select(grant => new { grant.RoleName, grant.Permission }).ToListAsync(ct);
        var rows = accounts.Take(25).Select(account =>
        {
            var roles = assignments.Where(link => link.UserId == account.Id).Select(link => link.Role).Order().ToArray();
            var permissions = account.IsActive ? grants.Where(grant => roles.Contains(grant.RoleName) &&
                (StorePermissions.CanAssignTo(grant.RoleName, grant.Permission) ||
                 grant.RoleName == StoreRoles.LegacyAdmin && StorePermissions.All.Contains(grant.Permission)))
                .Select(grant => grant.Permission).Distinct().Order().ToArray() : Array.Empty<string>();
            return new EffectivePermissionRow(account.Id, account.IsActive, roles, permissions);
        }).ToArray();
        return View(new EffectivePermissionsViewModel(rows, page, accounts.Count > 25, userId));
    }
}

public sealed record EffectivePermissionRow(string UserId, bool Active, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record EffectivePermissionsViewModel(IReadOnlyList<EffectivePermissionRow> Rows, int Page, bool HasNext, string? UserId);

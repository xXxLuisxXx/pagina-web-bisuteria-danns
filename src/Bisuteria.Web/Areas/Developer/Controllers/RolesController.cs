using Bisuteria.Domain.Security;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bisuteria.Web.Services;
using System.Text.Json;

namespace Bisuteria.Web.Areas.Developer.Controllers;

[Area("Developer")]
[Authorize(Policy = StorePermissions.RolesViewMatrix)]
public sealed class RolesController(AppDbContext db, UserManager<ApplicationUser> users, SecurityAuditLogger audit) : Controller
{
    private static readonly string[] EditableRoles = [StoreRoles.Owner, StoreRoles.Operator];

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var matrix = await db.RolePermissions.AsNoTracking()
            .Where(x => EditableRoles.Contains(x.RoleName))
            .GroupBy(x => x.RoleName)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Permission).ToHashSet(), ct);
        return View(new RoleMatrixViewModel(StorePermissions.All, matrix));
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.RolesEditMatrix)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string role, string[]? permissions, CancellationToken ct)
    {
        if (!EditableRoles.Contains(role, StringComparer.Ordinal) || permissions is null ||
            permissions.Length == 0 || permissions.Any(p => !StorePermissions.All.Contains(p, StringComparer.Ordinal)))
            return BadRequest("Rol o permisos no válidos.");

        var requested = permissions.ToHashSet(StringComparer.Ordinal);
        var currentRows = await db.RolePermissions.Where(x => x.RoleName == role).ToListAsync(ct);
        var before = currentRows.Select(x => x.Permission).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        db.RolePermissions.RemoveRange(currentRows.Where(x => !requested.Contains(x.Permission)));
        var current = currentRows.Select(x => x.Permission).ToHashSet(StringComparer.Ordinal);
        db.RolePermissions.AddRange(requested.Except(current).Select(p => new RolePermission
        {
            RoleName = role,
            Permission = p
        }));

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            var roleUsers = await (from user in db.Users
                                   join userRole in db.UserRoles on user.Id equals userRole.UserId
                                   join identityRole in db.Roles on userRole.RoleId equals identityRole.Id
                                   where identityRole.Name == role
                                   select user).ToListAsync(ct);
            foreach (var user in roleUsers)
            {
                var result = await users.UpdateSecurityStampAsync(user);
                if (!result.Succeeded) throw new InvalidOperationException("No se pudieron invalidar sesiones del rol actualizado.");
            }
            await audit.RecordAsync("Roles.MatrixChanged", "RolePermission", role, "200",
                JsonSerializer.Serialize(before), JsonSerializer.Serialize(requested.OrderBy(x => x, StringComparer.Ordinal).ToArray()), ct);
            await transaction.CommitAsync(ct);
        });
        TempData["Success"] = "Matriz actualizada; se cerrarán las sesiones activas del rol.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed record RoleMatrixViewModel(
    IReadOnlyList<string> Permissions,
    IReadOnlyDictionary<string, HashSet<string>> Grants);

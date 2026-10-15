using System.Security.Claims;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Bisuteria.Web.Services;

public sealed class SecurityAuditLogger(AppDbContext db, UserManager<ApplicationUser> users, IHttpContextAccessor accessor)
{
    public async Task PrivilegedLoginAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var roles = await users.GetRolesAsync(user);
        var privileged = roles.Where(role => role is StoreRoles.Developer or StoreRoles.Owner or StoreRoles.Operator or StoreRoles.LegacyAdmin).ToArray();
        if (privileged.Length == 0) return;
        db.Add(new AuditLog
        {
            ActorId = user.Id,
            ActorRole = string.Join(",", privileged),
            Action = "Authentication.LoginSucceeded",
            Outcome = "200",
            RemoteIp = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task CustomerDataAccessAsync(string? targetUserId, string action, CancellationToken ct = default)
    {
        await RecordAsync(action, "Customer", targetUserId, "200", cancellationToken: ct);
    }

    public async Task RecordAsync(string action, string? entityType, string? entityId, string outcome,
        string? beforeJson = null, string? afterJson = null, CancellationToken cancellationToken = default)
    {
        var actor = accessor.HttpContext?.User;
        var actorId = actor?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId)) return;
        db.Add(new AuditLog
        {
            ActorId = actorId,
            ActorRole = string.Join(",", actor!.FindAll(ClaimTypes.Role).Select(x => x.Value).Take(4)),
            Action = action,
            EntityType = entityType,
            EntityId = entityId is { Length: <= 450 } ? entityId : null,
            Outcome = outcome,
            BeforeJson = beforeJson is { Length: <= 2000 } ? beforeJson : null,
            AfterJson = afterJson is { Length: <= 2000 } ? afterJson : null,
            RemoteIp = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

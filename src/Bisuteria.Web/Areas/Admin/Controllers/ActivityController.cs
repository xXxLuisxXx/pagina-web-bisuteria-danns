using System.Security.Claims;
using Bisuteria.Application.Services;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.DashboardView)]
public sealed class ActivityController(AppDbContext db) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId)) return Forbid();
        ViewData["Title"] = "Mi actividad";
        var rows = await db.Set<AuditLog>().AsNoTracking().Where(entry => entry.ActorId == actorId)
            .OrderByDescending(entry => entry.CreatedAt).ThenByDescending(entry => entry.Id).Take(100)
            .Select(entry => new OwnActivityRow(entry.CreatedAt, entry.Action, entry.EntityType, entry.Outcome)).ToListAsync(ct);
        return View(rows);
    }
}

public sealed record OwnActivityRow(DateTime AtUtc, string Action, string? EntityType, string Outcome)
{
    public DateTime AtLocal => SalesCriteria.Local(AtUtc);
}

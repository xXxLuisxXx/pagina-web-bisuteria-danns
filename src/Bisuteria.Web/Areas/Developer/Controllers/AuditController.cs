using System.Text;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Developer.Controllers;

[Area("Developer"), Authorize(Policy = StorePermissions.AuditView)]
public sealed class AuditController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? actor, string? action, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var query = Filter(actor, action, from, to);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        return View(new AuditListViewModel(rows, actor, action, from, to));
    }

    [HttpGet]
    [Authorize(Policy = StorePermissions.ReportsExport)]
    public async Task<IActionResult> Export(string? actor, string? action, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var rows = await Filter(actor, action, from, to).OrderByDescending(x => x.CreatedAt).Take(5000).ToListAsync(ct);
        var csv = new StringBuilder("Fecha UTC;Actor;Acción;Resultado\n");
        foreach (var row in rows)
            csv.AppendLine($"{row.CreatedAt:O};{Safe(row.ActorId)};{Safe(row.Action)};{Safe(row.Outcome)}");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"auditoria_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private IQueryable<Domain.Entities.AuditLog> Filter(string? actor, string? action, DateTime? from, DateTime? to)
    {
        var query = db.Set<Domain.Entities.AuditLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(x => x.ActorId.Contains(actor.Trim()));
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action.Trim()));
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.Date.AddDays(1));
        return query;
    }

    private static string Safe(string value)
    {
        value = value.TrimStart();
        return (value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@') ? "'" : "") + value.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
    }
}

public sealed record AuditListViewModel(IReadOnlyList<Domain.Entities.AuditLog> Rows,
    string? Actor, string? Action, DateTime? From, DateTime? To);

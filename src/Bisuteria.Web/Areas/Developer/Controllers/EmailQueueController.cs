using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Developer.Controllers;

[Area("Developer"), Authorize(Policy = StorePermissions.SystemView)]
public sealed class EmailQueueController(AppDbContext db, SecurityAuditLogger audit) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rows = await db.Set<EmailOutbox>().AsNoTracking()
            .Where(x => x.SentAt == null)
            .OrderByDescending(x => x.Attempts >= 8)
            .ThenBy(x => x.NextAttemptAt)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .Select(x => new EmailQueueRow(x.Id, x.Kind, x.Recipient, x.Attempts,
                x.NextAttemptAt, x.Attempts >= 8, x.LastError))
            .ToListAsync(ct);
        return View(rows.Select(row => row with { Recipient = MaskRecipient(row.Recipient) }).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryFailed(int id, CancellationToken ct)
    {
        var item = await db.Set<EmailOutbox>().SingleOrDefaultAsync(x => x.Id == id && x.SentAt == null && x.Attempts >= 8, ct);
        if (item is null) return NotFound();

        var previousAttempts = item.Attempts;
        item.Attempts = 0;
        item.NextAttemptAt = DateTime.UtcNow;
        item.LastError = null;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync("EmailOutbox.RetryRequested", "EmailOutbox", id.ToString(), "200",
            afterJson: $"{{\"previousAttempts\":{previousAttempts}}}", cancellationToken: ct);
        TempData["Success"] = "El correo volvió a la cola de envío.";
        return RedirectToAction(nameof(Index));
    }

    private static string MaskRecipient(string recipient)
    {
        var separator = recipient.LastIndexOf('@');
        if (separator <= 0 || separator == recipient.Length - 1) return "destinatario oculto";
        var domain = recipient[(separator + 1)..];
        var local = recipient[..separator];
        return $"{local[0]}***@{domain}";
    }
}

public sealed record EmailQueueRow(int Id, string Kind, string Recipient, int Attempts,
    DateTime NextAttemptAt, bool Failed, string? LastError);

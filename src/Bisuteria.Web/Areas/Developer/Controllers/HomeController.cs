using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Developer.Controllers;

[Area("Developer"), Authorize(Policy = StorePermissions.SystemView)]
public sealed class HomeController(AppDbContext db, IConfiguration configuration, IWebHostEnvironment environment,
    HealthCheckService healthChecks) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pending = await db.Set<Domain.Entities.EmailOutbox>().CountAsync(x => x.SentAt == null && x.Attempts < 8, ct);
        var failed = await db.Set<Domain.Entities.EmailOutbox>().CountAsync(x => x.SentAt == null && x.Attempts >= 8, ct);
        var storeRecipient = await db.SiteSettings.AsNoTracking().Where(x => x.Key == "ContactEmail")
            .Select(x => x.Value).SingleOrDefaultAsync(ct);
        var health = await healthChecks.CheckHealthAsync(ct);
        return View(new DeveloperSystemViewModel(
            environment.EnvironmentName,
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "desconocida",
            health.Status.ToString(), pending, failed,
            SmtpConfigurationStatus.Describe(configuration, environment),
            SmtpConfigurationStatus.DescribeQueue(configuration),
            SmtpConfigurationStatus.DescribeStoreNotifications(storeRecipient)));
    }
}

public sealed record DeveloperSystemViewModel(string Environment, string Version, string Health,
    int PendingEmails, int FailedEmails, string SmtpStatus, string QueueStatus, string StoreNotificationStatus);

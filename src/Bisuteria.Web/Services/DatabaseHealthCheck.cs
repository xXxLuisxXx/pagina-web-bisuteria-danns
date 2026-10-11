using Bisuteria.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bisuteria.Web.Services;

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { return await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(); }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}

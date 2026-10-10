using System.Security.Claims;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bisuteria.Web.Security;

public sealed class AdminAuditFilter(IServiceScopeFactory scopes, ILogger<AdminAuditFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || context.RouteData.Values["area"]?.ToString() != "Admin" || !context.HttpContext.User.IsInRole("Admin"))
        { await next(); return; }
        var result = await next();
        var action = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
        var actor = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new AuditLog { ActorId = actor, Action = action, Outcome = result.Exception is null ? "Solicitud procesada" : "Error" });
            await db.SaveChangesAsync();
            logger.LogInformation("Solicitud administrativa {Action}, actor {Actor}, UTC {Timestamp}", action, actor, DateTimeOffset.UtcNow);
        }
        catch (Exception) { logger.LogError("No se pudo persistir la auditoría de {Action}, actor {Actor}", action, actor); }
    }
}

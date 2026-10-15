using System.Security.Claims;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bisuteria.Web.Security;

public sealed class AdminAuditFilter(IServiceScopeFactory scopes, ILogger<AdminAuditFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var area = context.RouteData.Values["area"]?.ToString();
        var controller = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString();
        var administrative = area is "Admin" or "Developer" || controller == "StaffInvitation" && actionName is "Create" or "Revoke" or "SetActive";
        var export = HttpMethods.IsGet(context.HttpContext.Request.Method) && actionName?.StartsWith("Export", StringComparison.Ordinal) == true;
        if (!(HttpMethods.IsPost(context.HttpContext.Request.Method) && administrative || export) || context.HttpContext.User.Identity?.IsAuthenticated != true)
        { await next(); return; }
        var result = await next();
        var action = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
        var actor = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var roles = string.Join(",", context.HttpContext.User.FindAll(ClaimTypes.Role).Select(x => x.Value).Take(4));
        var entityId = context.RouteData.Values["id"]?.ToString();
        var statusCode = result.Result switch
        {
            Microsoft.AspNetCore.Mvc.StatusCodeResult status => status.StatusCode,
            Microsoft.AspNetCore.Mvc.ObjectResult value when value.StatusCode.HasValue => value.StatusCode.Value,
            _ => result.Exception is null ? 200 : 500
        };
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new AuditLog
            {
                ActorId = actor,
                ActorRole = roles,
                Action = action,
                EntityType = context.RouteData.Values["controller"]?.ToString(),
                EntityId = entityId is { Length: <= 450 } ? entityId : null,
                Outcome = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RemoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Solicitud administrativa {Action}, actor {Actor}, UTC {Timestamp}", action, actor, DateTimeOffset.UtcNow);
        }
        catch (Exception) { logger.LogError("No se pudo persistir la auditoría de {Action}, actor {Actor}", action, actor); }
    }
}

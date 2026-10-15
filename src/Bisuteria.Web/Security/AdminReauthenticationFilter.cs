using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bisuteria.Web.Security;

public sealed class AdminReauthenticationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var area = context.RouteData.Values["area"]?.ToString();
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        var privilegedPost = area is "Admin" or "Developer" || controller == "StaffInvitation" && action is "Create" or "Revoke" or "SetActive";
        var sensitiveExport = HttpMethods.IsGet(context.HttpContext.Request.Method) && action?.StartsWith("Export", StringComparison.Ordinal) == true;
        if (HttpMethods.IsPost(context.HttpContext.Request.Method) && privilegedPost || sensitiveExport)
        {
            var ticket = await context.HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (ticket.Properties?.IssuedUtc is not { } issued || DateTimeOffset.UtcNow - issued > TimeSpan.FromMinutes(15))
            {
                context.Result = new ContentResult { StatusCode = 403, Content = "Vuelve a iniciar sesión antes de realizar esta acción administrativa." };
                return;
            }
        }
        await next();
    }
}

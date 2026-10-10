using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bisuteria.Web.Security;

public sealed class AntispamFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        if (HttpMethods.IsPost(context.HttpContext.Request.Method) && context.HttpContext.Request.HasFormContentType &&
            (controller == "Contact" || (controller == "Account" && action == "Register")))
        {
            var form = await context.HttpContext.Request.ReadFormAsync(context.HttpContext.RequestAborted);
            if (!string.IsNullOrEmpty(form["Website"])) { context.Result = new BadRequestObjectResult("No se pudo procesar el formulario."); return; }
        }
        await next();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bisuteria.Web.Filters;

// Valida argumentos simples antes de consultar o modificar datos; los formularios conservan sus errores por campo.
public sealed class EndpointValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var (key, value) in context.ActionArguments)
        {
            if (value is int id && (key.Equals("id", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Id", StringComparison.Ordinal)) && id <= 0)
            { context.Result = new NotFoundResult(); return; }
            if (value is Enum enumeration && !Enum.IsDefined(enumeration.GetType(), enumeration))
                context.ModelState.AddModelError(key, "Selecciona una opción válida.");
            if (key == "page" && value is int page && (page < 1 || page > 10000))
                context.ModelState.AddModelError(key, "La página debe estar entre 1 y 10000.");
            if (value is string text && text.Length > (key is "adminNotes" or "reason" ? 1000 : key == "token" ? 4096 : key == "returnUrl" ? 2048 : 200))
                context.ModelState.AddModelError(key, "El texto supera la longitud permitida.");
            if (value is DateTime date && (date.Year < 2000 || date.Year > 2100))
                context.ModelState.AddModelError(key, "Usa una fecha entre 2000 y 2100.");
        }
        if (context.ActionArguments.TryGetValue("from", out var start) && start is DateTime from && context.ActionArguments.TryGetValue("to", out var end) && end is DateTime to && from > to)
            context.ModelState.AddModelError("to", "La fecha final no puede ser anterior al inicio.");
        if (!context.ModelState.IsValid && (HttpMethods.IsGet(context.HttpContext.Request.Method) || context.ActionArguments.Values.All(v => v is null || v is string || v.GetType().IsValueType)))
        { context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState) { Title = "Revisa los parámetros de la solicitud." }); return; }
        await next();
    }
}

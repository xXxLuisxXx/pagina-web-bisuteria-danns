using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Filters;

public sealed class PersistenceExceptionFilter(ILogger<PersistenceExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateException) return;
        logger.LogWarning("No se pudo guardar una operación de {Controller}; tipo {ErrorType}",
            context.RouteData.Values["controller"], context.Exception.GetType().Name);
        context.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentType = "text/plain; charset=utf-8",
            Content = context.Exception is DbUpdateConcurrencyException
                ? "Los datos cambiaron durante la edición. Recarga la página antes de guardar."
                : "No se pudo guardar. Comprueba que no haya datos duplicados y recarga la página."
        };
        context.ExceptionHandled = true;
    }
}

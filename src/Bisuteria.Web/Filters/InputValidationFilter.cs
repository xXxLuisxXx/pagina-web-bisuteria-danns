using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Bisuteria.Web.Filters;

// Ejecuta los validadores registrados antes de que la acción consulte ModelState.
public sealed class InputValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var value in context.ActionArguments.Values.Where(x => x is not null))
        {
            var type = typeof(IValidator<>).MakeGenericType(value!.GetType());
            if (context.HttpContext.RequestServices.GetService(type) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(value), context.HttpContext.RequestAborted);
                foreach (var error in result.Errors)
                    context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
        }
        await next();
    }
}

using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bisuteria.Web.Security;

public sealed class InitialPasswordFilter(UserManager<ApplicationUser> users) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var user = await users.GetUserAsync(context.HttpContext.User);
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            if (user?.MustChangePassword == true && !(controller == "Account" && action is "ChangePassword" or "Logout"))
            {
                context.Result = new RedirectToActionResult("ChangePassword", "Account", new { area = "" });
                return;
            }
            if (user?.RequireTwoFactorSetup == true && !await users.GetTwoFactorEnabledAsync(user) &&
                !(controller == "Account" && action is "Authenticator" or "ChangePassword" or "Logout"))
            {
                context.Result = new RedirectToActionResult("Authenticator", "Account", new { area = "" });
                return;
            }
        }
        await next();
    }
}

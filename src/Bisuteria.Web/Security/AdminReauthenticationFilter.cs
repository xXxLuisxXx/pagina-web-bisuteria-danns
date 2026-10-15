using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Encodings.Web;

namespace Bisuteria.Web.Security;

public sealed class AdminReauthenticationFilter(TimeProvider timeProvider) : IAsyncActionFilter
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
            var age = ticket.Properties?.IssuedUtc is { } issued
                ? timeProvider.GetUtcNow() - issued
                : (TimeSpan?)null;
            if (age is null || age < TimeSpan.Zero || age > TimeSpan.FromMinutes(15))
            {
                var request = context.HttpContext.Request;
                var returnUrl = GetSafeReturnUrl(request, HttpMethods.IsGet(request.Method));
                var encodedReturnUrl = UrlEncoder.Default.Encode(returnUrl);
                var reauthenticationUrl = $"{request.PathBase}/Account/Login?reauthenticationRequired=true&returnUrl={encodedReturnUrl}";
                context.Result = new RedirectResult(reauthenticationUrl);
                return;
            }
        }
        await next();
    }

    private static string GetSafeReturnUrl(HttpRequest request, bool currentRequestIsGet)
    {
        if (currentRequestIsGet)
            return request.PathBase + request.Path + request.QueryString;

        var referer = request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var absolute) &&
            string.Equals(absolute.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(absolute.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(absolute.UserInfo))
            return absolute.PathAndQuery;

        if (referer.StartsWith('/') && !referer.StartsWith("//", StringComparison.Ordinal) && !referer.StartsWith("/\\", StringComparison.Ordinal))
            return referer;

        var area = request.Path.StartsWithSegments("/Developer") ? "/Developer" : "/Admin";
        return request.PathBase + area;
    }
}

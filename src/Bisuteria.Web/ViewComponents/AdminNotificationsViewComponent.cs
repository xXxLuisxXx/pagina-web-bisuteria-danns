using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Bisuteria.Domain.Security;
using Bisuteria.Application.DTOs.Admin;
namespace Bisuteria.Web.ViewComponents;
public sealed class AdminNotificationsViewComponent(IAdminService admin, IMemoryCache cache, IAuthorizationService authorization) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!(await authorization.AuthorizeAsync(HttpContext.User, resource: null, StorePermissions.AdminNotifications)).Succeeded) return Content("");
        var data = await cache.GetOrCreateAsync("dashboard:::day:", async entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30); return await admin.GetDashboardAsync(HttpContext.RequestAborted); });
        var alerts = new List<DashboardAlert>();
        foreach (var alert in data!.Alerts)
            if (alert.Count > 0 && (await authorization.AuthorizeAsync(HttpContext.User, null, alert.Permission)).Succeeded)
                alerts.Add(alert);
        return View(new AdminNotificationSummary(alerts));
    }
}

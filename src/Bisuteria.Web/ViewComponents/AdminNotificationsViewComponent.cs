using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
namespace Bisuteria.Web.ViewComponents;
public sealed class AdminNotificationsViewComponent(IAdminService admin, IMemoryCache cache) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.IsInRole("Admin")) return Content("");
        var data = await cache.GetOrCreateAsync("dashboard:::day:", async entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30); return await admin.GetDashboardAsync(HttpContext.RequestAborted); });
        return View(data!);
    }
}

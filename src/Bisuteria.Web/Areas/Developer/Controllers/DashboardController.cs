using Bisuteria.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Developer.Controllers;

[Area("Developer"), Authorize(Policy = StorePermissions.SystemView)]
public sealed class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Home", new { area = "Developer" });
}

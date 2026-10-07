using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public abstract class AdminBaseController : Controller
{
    protected IActionResult RedirectToDashboard() => RedirectToAction(nameof(DashboardController.Index), "Dashboard", new { area = "Admin" });
}

using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class DashboardController : AdminBaseController
{
    private readonly IAdminService _admin;

    public DashboardController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Panel de administración";
        var stats = await _admin.GetDashboardAsync(ct);
        return View(stats);
    }
}

using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace Bisuteria.Web.Areas.Admin.Controllers;
public sealed class SearchController(IAdminService admin) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Title"] = "Búsqueda del panel";
        ViewData["Query"] = q;
        return View(await admin.SearchAsync(q ?? "", ct));
    }
}

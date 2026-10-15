using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bisuteria.Domain.Security;
namespace Bisuteria.Web.Areas.Admin.Controllers;
[Authorize(Policy = StorePermissions.AdminSearch)]
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

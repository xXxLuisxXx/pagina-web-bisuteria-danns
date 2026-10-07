using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class UsersController : AdminBaseController
{
    private readonly IAdminService _admin;

    public UsersController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Usuarios";
        var users = await _admin.GetUsersAsync(ct);
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken ct)
    {
        var users = await _admin.GetUsersAsync(ct);
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        var ok = await _admin.SetUserActiveAsync(id, !user.IsActive, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Usuario actualizado." : "No se puede desactivar una cuenta administradora.";
        return RedirectToAction(nameof(Index));
    }
}

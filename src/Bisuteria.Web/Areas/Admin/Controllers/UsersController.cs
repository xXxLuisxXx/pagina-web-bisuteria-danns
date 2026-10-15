using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.CustomersViewLimited)]
public class UsersController : AdminBaseController
{
    private readonly IAdminService _admin;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SecurityAuditLogger _audit;

    public UsersController(IAdminService admin, UserManager<ApplicationUser> users, SecurityAuditLogger audit)
    {
        _admin = admin;
        _users = users;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, int page = 1)
    {
        ViewData["Title"] = "Usuarios";
        ViewBag.Page = page;
        var users = (await _admin.GetUsersAsync(ct, page)).ToList();
        if (User.IsInRole(StoreRoles.Owner))
            users = users.Where(user => user.Roles.Contains(StoreRoles.Customer) || user.Roles.Contains(StoreRoles.Operator)).ToList();
        if (User.IsInRole(StoreRoles.Developer) || User.IsInRole(StoreRoles.Owner))
        {
            await _audit.CustomerDataAccessAsync(null, "CustomerData.ListViewed", ct);
            foreach (var user in users)
            {
                user.Email = MaskEmail(user.Email);
                user.Phone = MaskPhone(user.Phone);
                user.City = string.IsNullOrWhiteSpace(user.City) ? null : "Oculta";
                user.Province = string.IsNullOrWhiteSpace(user.Province) ? null : "Oculta";
            }
        }
        return View(users);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = StorePermissions.CustomersViewLimited)]
    public async Task<IActionResult> Reveal(string id, CancellationToken ct)
    {
        if (!User.IsInRole(StoreRoles.Developer)) return Forbid();
        var user = await _users.FindByIdAsync(id);
        if (user is null || !await _users.IsInRoleAsync(user, StoreRoles.Customer)) return NotFound();
        await _audit.CustomerDataAccessAsync(user.Id, "CustomerData.Revealed", ct);
        return View(new RevealedCustomerViewModel(user.Email ?? "", user.PhoneNumber, user.City, user.Province));
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.UsersManageStaff)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken ct)
    {
        var target = await _users.FindByIdAsync(id);
        if (target is null) return NotFound();
        var targetRoles = await _users.GetRolesAsync(target);
        var isDeveloper = User.IsInRole(StoreRoles.Developer);
        if (targetRoles.Contains(StoreRoles.Developer))
        {
            if (!isDeveloper) return Forbid();
            if (target.IsActive && (await _users.GetUsersInRoleAsync(StoreRoles.Developer)).Count(x => x.IsActive) <= 1)
            {
                TempData["Error"] = "No se puede desactivar al último Developer activo.";
                return RedirectToAction(nameof(Index));
            }
        }
        else if (targetRoles.Contains(StoreRoles.Owner) && !isDeveloper) return Forbid();
        else if (!targetRoles.Any(role => role is StoreRoles.Owner or StoreRoles.Operator)) return Forbid();

        var users = await _admin.GetUsersAsync(ct, userId: id);
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        var ok = await _admin.SetUserActiveAsync(id, !user.IsActive, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Usuario actualizado." : "No se pudo actualizar la cuenta.";
        return RedirectToAction(nameof(Index));
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "Oculto";
        return email[..1] + "***" + email[at..];
    }

    private static string? MaskPhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) ? null : new string('*', Math.Max(0, phone.Length - 2)) + phone[^Math.Min(2, phone.Length)..];
}

public sealed record RevealedCustomerViewModel(string Email, string? Phone, string? City, string? Province);

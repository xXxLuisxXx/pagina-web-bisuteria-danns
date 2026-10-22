using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Models;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.CustomersViewLimited)]
public class UsersController : AdminBaseController
{
    private readonly IAdminService _admin;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SecurityAuditLogger _audit;
    private readonly AppDbContext _db;

    public UsersController(IAdminService admin, UserManager<ApplicationUser> users, SecurityAuditLogger audit, AppDbContext db)
    {
        _admin = admin;
        _users = users;
        _audit = audit;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, int page = 1)
    {
        ViewData["Title"] = "Usuarios";
        ViewBag.Page = page;
        var users = (await _admin.GetUsersAsync(ct, page, pageSize: 26, businessAccountsOnly: User.IsInRole(StoreRoles.Owner))).ToList();
        ViewBag.HasNext = users.Count > 25;
        if (users.Count > 25) users.RemoveAt(users.Count - 1);
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
        if (user is null || !await IsCustomerAccountAsync(user)) return NotFound();
        await _audit.CustomerDataAccessAsync(user.Id, "CustomerData.Revealed", ct);
        return View(new RevealedCustomerViewModel(user.Email ?? "", user.PhoneNumber, user.City, user.Province));
    }

    [HttpGet]
    [Authorize(Policy = StorePermissions.UsersManageStaff)]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        if (!CanManageCustomerAccounts()) return Forbid();
        var user = await _users.FindByIdAsync(id);
        if (user is null || !await IsCustomerAccountAsync(user)) return NotFound();

        await _audit.CustomerDataAccessAsync(user.Id, "CustomerData.EditViewed", ct);
        ViewBag.Provinces = Bisuteria.Domain.Enums.EcuadorProvinces.All;
        return View(new AdminUserEditViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.PhoneNumber,
            City = user.City,
            Province = user.Province
        });
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.UsersManageStaff)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, AdminUserEditViewModel model, CancellationToken ct)
    {
        if (!CanManageCustomerAccounts()) return Forbid();
        // Los valores del formulario pueden sobreescribir un parámetro simple durante el model binding.
        // Obtén el objetivo desde la ruta o query y compáralo con el campo oculto antes de actualizar.
        var targetId = RouteData.Values["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(targetId)) targetId = Request.Query["id"].ToString();
        if (string.IsNullOrWhiteSpace(targetId) || !string.Equals(targetId, model.Id, StringComparison.Ordinal)) return NotFound();
        var user = await _users.FindByIdAsync(targetId);
        if (user is null || !await IsCustomerAccountAsync(user)) return NotFound();
        ViewBag.Provinces = Bisuteria.Domain.Enums.EcuadorProvinces.All;
        if (!ModelState.IsValid) return View(model);

        var newEmail = model.Email.Trim();
        var emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var existingAccount = await _users.FindByEmailAsync(newEmail);
            if (existingAccount is not null && existingAccount.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "Ese correo ya está registrado en otra cuenta.");
                return View(model);
            }
        }

        var changedFields = new List<string>();
        if (emailChanged) changedFields.Add("Email");
        if (user.FirstName != model.FirstName.Trim()) changedFields.Add("FirstName");
        if (user.LastName != model.LastName.Trim()) changedFields.Add("LastName");
        if (user.PhoneNumber != model.Phone?.Trim()) changedFields.Add("PhoneNumber");
        if (user.City != model.City?.Trim()) changedFields.Add("City");
        if (user.Province != model.Province?.Trim()) changedFields.Add("Province");

        if (emailChanged)
        {
            user.Email = newEmail;
            user.UserName = newEmail;
            user.EmailConfirmed = false;
        }
        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        user.City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim();
        user.Province = string.IsNullOrWhiteSpace(model.Province) ? null : model.Province.Trim();

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(
                    error.Code is "DuplicateEmail" or "DuplicateUserName" ? nameof(model.Email) : string.Empty,
                    error.Code is "DuplicateEmail" or "DuplicateUserName"
                        ? "Ese correo ya está registrado en otra cuenta."
                        : error.Description);
            return View(model);
        }

        if (emailChanged)
            await HttpContext.RequestServices.GetRequiredService<PasswordRecoveryQueue>().EnqueueAsync(newEmail, confirm: true);

        await _audit.RecordAsync("CustomerData.Updated", "Customer", user.Id, "200",
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { ChangedFields = changedFields }), cancellationToken: ct);
        TempData["Success"] = emailChanged
            ? "La información se actualizó; se envió un enlace de confirmación al nuevo correo. Los pedidos se conservaron."
            : "La información del cliente se actualizó. Su historial de pedidos se conservó.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = StorePermissions.UsersManageStaff)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!CanManageCustomerAccounts()) return Forbid();
        var user = await _users.FindByIdAsync(id);
        if (user is null || !await IsCustomerAccountAsync(user)) return NotFound();

        await _audit.CustomerDataAccessAsync(user.Id, "CustomerData.DeleteViewed", ct);
        return View(new AdminUserDeleteViewModel { Id = user.Id, Email = user.Email ?? string.Empty });
    }

    [HttpPost]
    [Authorize(Policy = StorePermissions.UsersManageStaff)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(AdminUserDeleteViewModel model, CancellationToken ct)
    {
        if (!CanManageCustomerAccounts()) return Forbid();
        var user = await _users.FindByIdAsync(model.Id);
        if (user is null || !await IsCustomerAccountAsync(user)) return NotFound();

        model.Email = user.Email ?? string.Empty;
        if (!ModelState.IsValid || !string.Equals(model.ConfirmationEmail.Trim(), model.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (ModelState.IsValid)
                ModelState.AddModelError(nameof(model.ConfirmationEmail), "El correo no coincide con el cliente seleccionado.");
            return View(model);
        }

        var userId = user.Id;
        var addressesRemoved = 0;
        var favoritesRemoved = 0;
        var ordersPreserved = 0;
        var reviewsAnonymized = 0;
        IdentityResult? deleteResult = null;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            var addresses = await _db.Addresses.Where(x => x.UserId == userId).ToListAsync(ct);
            var favorites = await _db.Favorites.Where(x => x.UserId == userId).ToListAsync(ct);
            var orders = await _db.Orders.Where(x => x.UserId == userId).ToListAsync(ct);
            var reviews = await _db.Reviews.Where(x => x.UserId == userId).ToListAsync(ct);

            _db.Addresses.RemoveRange(addresses);
            _db.Favorites.RemoveRange(favorites);
            foreach (var order in orders)
            {
                order.UserId = null;
                order.CustomerName = "Cliente eliminado";
                order.CustomerEmail = string.Empty;
                order.CustomerPhone = string.Empty;
                order.AddressLine1 = string.Empty;
                order.AddressLine2 = null;
                order.City = string.Empty;
                order.Province = string.Empty;
                order.PostalCode = null;
                order.Observations = null;
                order.GiftRecipientName = null;
                order.GiftMessage = null;
            }
            foreach (var review in reviews)
            {
                review.UserId = null;
                review.CustomerName = "Cliente eliminado";
                review.CustomerPhotoUrl = null;
            }

            await _db.SaveChangesAsync(ct);
            deleteResult = await _users.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            addressesRemoved = addresses.Count;
            favoritesRemoved = favorites.Count;
            ordersPreserved = orders.Count;
            reviewsAnonymized = reviews.Count;
            await _audit.RecordAsync("CustomerAccount.Deleted", "Customer", userId, "200",
                afterJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    AddressesRemoved = addressesRemoved,
                    FavoritesRemoved = favoritesRemoved,
                    OrdersPreserved = ordersPreserved,
                    ReviewsAnonymized = reviewsAnonymized
                }), cancellationToken: ct);
            await transaction.CommitAsync(ct);
        });

        if (deleteResult is null || !deleteResult.Succeeded)
        {
            foreach (var error in deleteResult?.Errors ?? Array.Empty<IdentityError>())
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["Success"] = "La cuenta se eliminó; los pedidos conservan sus productos y montos con los datos personales anonimizados, y las reseñas quedaron desvinculadas.";
        return RedirectToAction(nameof(Index));
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
        var isOwner = User.IsInRole(StoreRoles.Owner);
        var isCustomer = targetRoles.Count == 1 && targetRoles[0] == StoreRoles.Customer;
        if (isCustomer)
        {
            if (!isDeveloper && !isOwner) return Forbid();
        }
        else if (targetRoles.Contains(StoreRoles.Developer))
        {
            if (!isDeveloper) return Forbid();
            if (target.IsActive && (await _users.GetUsersInRoleAsync(StoreRoles.Developer)).Count(x => x.IsActive) <= 1)
            {
                TempData["Error"] = "No se puede desactivar al último Developer activo.";
                return RedirectToAction(nameof(Index));
            }
        }
        else if (targetRoles.Contains(StoreRoles.Customer)) return Forbid();
        else if (targetRoles.Contains(StoreRoles.Owner) && !isDeveloper) return Forbid();
        else if (!targetRoles.Any(role => role is StoreRoles.Owner or StoreRoles.Operator or StoreRoles.Customer)) return Forbid();

        var users = await _admin.GetUsersAsync(ct, userId: id);
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        var ok = await _admin.SetUserActiveAsync(id, !user.IsActive, ct);
        if (ok && targetRoles.Contains(StoreRoles.Customer))
            await _audit.RecordAsync("CustomerAccount.StateChanged", "Customer", id, "200",
                afterJson: System.Text.Json.JsonSerializer.Serialize(new { IsActive = !user.IsActive }), cancellationToken: ct);
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

    private async Task<bool> IsCustomerAccountAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return roles.Count == 1 && roles[0] == StoreRoles.Customer;
    }

    private bool CanManageCustomerAccounts()
        => User.IsInRole(StoreRoles.Developer) || User.IsInRole(StoreRoles.Owner);
}

public sealed record RevealedCustomerViewModel(string Email, string? Phone, string? City, string? Province);

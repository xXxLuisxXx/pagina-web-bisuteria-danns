using System.Security.Cryptography;
using System.Text;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Bisuteria.Web.Services;

namespace Bisuteria.Web.Controllers;

[Authorize]
public sealed class StaffInvitationController(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    IAuthorizationService authorization,
    IConfiguration configuration,
    SecurityAuditLogger audit,
    IDataProtectionProvider dataProtection,
    Bisuteria.Application.Interfaces.IAdminService admin) : Controller
{
    private const string InviteEmailProtectionPurpose = "BisuteriasDanns.StaffInvitation.Email.v1";
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(string token, CancellationToken ct)
    {
        var invitation = await FindValidAsync(token, ct);
        if (invitation is null) return View("Invalid");
        return View(new StaffInvitationViewModel { Token = token, Email = invitation.Email, RoleName = invitation.RoleName });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(StaffInvitationViewModel model, CancellationToken ct)
    {
        var invitation = await FindValidAsync(model.Token, ct);
        if (invitation is null) return View("Invalid");
        model.Email = invitation.Email;
        model.RoleName = invitation.RoleName;
        if (!ModelState.IsValid) return View(model);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var tokenHash = HashToken(model.Token);
            var claimed = await db.StaffInvitations.Where(x => x.TokenHash == tokenHash && x.AcceptedAtUtc == null &&
                    x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AcceptedAtUtc, DateTime.UtcNow), ct);
            if (claimed != 1) return View("Invalid");
            if (await users.FindByEmailAsync(invitation.Email) is not null)
            {
                await transaction.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, "Esta cuenta ya fue activada. Solicita una nueva invitación si necesitas ayuda.");
                return View("Invalid");
            }

            var user = new ApplicationUser
            {
                UserName = invitation.Email,
                Email = invitation.Email,
                EmailConfirmed = true,
                IsActive = true,
                RequireTwoFactorSetup = invitation.RoleName == StoreRoles.Owner
            };
            var create = await users.CreateAsync(user, model.Password);
            if (!create.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                foreach (var error in create.Errors) ModelState.AddModelError(nameof(model.Password), error.Description);
                return View(model);
            }
            var addRole = await users.AddToRoleAsync(user, invitation.RoleName);
            if (!addRole.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, "No se pudo completar la activación. Contacta al administrador.");
                return View(model);
            }
            db.Add(new AuditLog
            {
                ActorId = user.Id,
                ActorRole = invitation.RoleName,
                Action = "StaffInvitation.Accepted",
                EntityType = "User",
                EntityId = user.Id,
                Outcome = "200",
                RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return View("Accepted", model);
        });
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var canOwner = await authorization.AuthorizeAsync(User, null, StorePermissions.UsersManageOwner);
        var canOperator = await authorization.AuthorizeAsync(User, null, StorePermissions.UsersManageStaff);
        if (!canOwner.Succeeded && !canOperator.Succeeded) return Forbid();
        var rows = await db.StaffInvitations.AsNoTracking()
            .Where(x => x.AcceptedAtUtc == null && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .Where(x => (x.RoleName == StoreRoles.Owner && canOwner.Succeeded) || (x.RoleName == StoreRoles.Operator && canOperator.Succeeded))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StaffInvitationRow(x.Id, x.Email, x.RoleName, x.CreatedAt, x.ExpiresAtUtc))
            .ToListAsync(ct);
        var members = new List<StaffMemberRow>();
        if (canOwner.Succeeded)
        {
            members.AddRange((await users.GetUsersInRoleAsync(StoreRoles.Owner)).Select(x => new StaffMemberRow(x.Id, x.Email ?? "", StoreRoles.Owner, x.IsActive)));
            members.AddRange((await users.GetUsersInRoleAsync(StoreRoles.Operator)).Select(x => new StaffMemberRow(x.Id, x.Email ?? "", StoreRoles.Operator, x.IsActive)));
        }
        else if (canOperator.Succeeded)
            members.AddRange((await users.GetUsersInRoleAsync(StoreRoles.Operator)).Select(x => new StaffMemberRow(x.Id, x.Email ?? "", StoreRoles.Operator, x.IsActive)));
        return View(new StaffManagementViewModel(rows, members.OrderBy(x => x.RoleName).ThenBy(x => x.Email).ToList()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string roleName, CancellationToken ct)
    {
        email = (email ?? string.Empty).Trim();
        if (email.Length > 254 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            ModelState.AddModelError(nameof(email), "Escribe un correo válido.");
        if (roleName is not (StoreRoles.Owner or StoreRoles.Operator))
            ModelState.AddModelError(nameof(roleName), "El rol solicitado no está permitido.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var requiredPermission = roleName == StoreRoles.Owner ? StorePermissions.UsersManageOwner : StorePermissions.UsersManageStaff;
        if (!(await authorization.AuthorizeAsync(User, null, requiredPermission)).Succeeded) return Forbid();
        if (await users.FindByEmailAsync(email) is not null)
            return Conflict("Ya existe una cuenta con ese correo.");

        var origin = configuration["PublicBaseUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            return StatusCode(503, "El origen HTTPS público no está configurado.");

        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(actorId)) return Forbid();
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var hash = HashToken(rawToken);
        var now = DateTime.UtcNow;
        var pending = await db.StaffInvitations.Where(x => x.Email == email && x.RoleName == roleName &&
            x.AcceptedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var old in pending) { old.RevokedAtUtc = now; old.UpdatedAt = now; }
        var invitation = new StaffInvitation
        {
            Email = email,
            RoleName = roleName,
            TokenHash = hash,
            InvitedByUserId = actorId,
            ExpiresAtUtc = now.AddHours(24),
            CreatedAt = now
        };
        var url = QueryHelpers.AddQueryString(new Uri(baseUri, "/StaffInvitation/Accept").AbsoluteUri, "token", rawToken);
        db.StaffInvitations.Add(invitation);
        var emailBody = $"Te invitaron a colaborar como {roleName}. Activa tu cuenta dentro de 24 horas y elige tu propia contraseña: {url}";
        db.Set<EmailOutbox>().Add(new EmailOutbox
        {
            Recipient = email,
            Kind = "StaffInvitation",
            Subject = "Invitación para activar tu cuenta de Bisuterías Dann’s",
            TextBody = dataProtection.CreateProtector(InviteEmailProtectionPurpose).Protect(emailBody)
        });
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync("StaffInvitation.Created", "StaffInvitation", invitation.Id.ToString(), "200",
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { Role = roleName }), cancellationToken: ct);
        TempData["Success"] = "Invitación registrada y correo agregado a la cola de salida.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        var invitation = await db.StaffInvitations.FindAsync([id], ct);
        if (invitation is null || invitation.AcceptedAtUtc is not null) return NotFound();
        var permission = invitation.RoleName == StoreRoles.Owner ? StorePermissions.UsersManageOwner : StorePermissions.UsersManageStaff;
        if (!(await authorization.AuthorizeAsync(User, null, permission)).Succeeded) return Forbid();
        var previousState = invitation.RevokedAtUtc is null ? "Active" : "Revoked";
        invitation.RevokedAtUtc = DateTime.UtcNow;
        invitation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync("StaffInvitation.Revoked", "StaffInvitation", invitation.Id.ToString(), "200",
            System.Text.Json.JsonSerializer.Serialize(new { State = previousState }), System.Text.Json.JsonSerializer.Serialize(new { State = "Revoked" }), ct);
        TempData["Success"] = "Invitación desactivada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool active, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var roles = await users.GetRolesAsync(user);
        var isDeveloper = User.IsInRole(StoreRoles.Developer);
        var canManageOwner = (await authorization.AuthorizeAsync(User, null, StorePermissions.UsersManageOwner)).Succeeded;
        var canManageStaff = (await authorization.AuthorizeAsync(User, null, StorePermissions.UsersManageStaff)).Succeeded;
        if (roles.Contains(StoreRoles.Developer) && !isDeveloper) return Forbid();
        if (roles.Contains(StoreRoles.Owner) && !canManageOwner) return Forbid();
        if (roles.Contains(StoreRoles.Operator) && !canManageStaff) return Forbid();
        if (!roles.Any(r => r is StoreRoles.Developer or StoreRoles.Owner or StoreRoles.Operator)) return NotFound();
        if (!active && roles.Contains(StoreRoles.Developer))
        {
            var activeDevelopers = (await users.GetUsersInRoleAsync(StoreRoles.Developer)).Count(x => x.IsActive);
            if (activeDevelopers <= 1)
            {
                TempData["Error"] = "No se puede desactivar al último Developer activo.";
                return RedirectToAction(nameof(Index));
            }
        }
        var previousActive = user.IsActive;
        if (!await admin.SetUserActiveAsync(id, active, ct))
            return Conflict("No se pudo cambiar el estado; debe conservarse al menos un Developer activo.");
        await audit.RecordAsync("StaffAccount.StateChanged", "User", user.Id, "200",
            System.Text.Json.JsonSerializer.Serialize(new { Active = previousActive }), System.Text.Json.JsonSerializer.Serialize(new { Active = active }), ct);
        TempData["Success"] = active ? "Cuenta reactivada." : "Cuenta desactivada y sesiones invalidadas.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<StaffInvitation?> FindValidAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var hash = HashToken(token);
        return await db.StaffInvitations.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash &&
            x.AcceptedAtUtc == null && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow, ct);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record StaffInvitationRow(int Id, string Email, string RoleName, DateTime CreatedAt, DateTime ExpiresAtUtc);
public sealed record StaffMemberRow(string Id, string Email, string RoleName, bool IsActive);
public sealed record StaffManagementViewModel(IReadOnlyList<StaffInvitationRow> Invitations, IReadOnlyList<StaffMemberRow> Members);

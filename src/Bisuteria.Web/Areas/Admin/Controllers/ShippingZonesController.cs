using System.Globalization;
using System.Text.Json;
using Bisuteria.Application.Validators;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Security;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Web.Models;
using Bisuteria.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Web.Areas.Admin.Controllers;

[Authorize(Policy = StorePermissions.ShippingManage)]
public sealed class ShippingZonesController(AppDbContext db, SecurityAuditLogger audit) : AdminBaseController
{
    private const string ProvinceName = "Manabí";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Zonas y tarifas";
        var zones = await db.DeliveryZones.AsNoTracking()
            .OrderBy(zone => zone.SortOrder).ThenBy(zone => zone.City).ThenBy(zone => zone.Sector)
            .ToListAsync(ct);
        return View(zones);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nueva zona de entrega";
        return View("Edit", new ShippingZoneInputModel());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var zone = await db.DeliveryZones.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (zone is null) return NotFound();
        ViewData["Title"] = "Editar zona de entrega";
        return View(new ShippingZoneInputModel
        {
            Id = zone.Id, City = zone.City, Sector = zone.Sector, Cost = zone.Cost,
            IsFree = zone.IsFree, RequiresConfirmation = zone.RequiresConfirmation,
            EstimatedTime = zone.EstimatedTime, IsActive = zone.IsActive, SortOrder = zone.SortOrder
        });
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Edit(ShippingZoneInputModel model, CancellationToken ct)
    {
        ViewData["Title"] = model.Id == 0 ? "Nueva zona de entrega" : "Editar zona de entrega";
        if (!ModelState.IsValid) return View(model);

        model.Sector = DeliverySectorRules.Clean(model.Sector);
        model.EstimatedTime = string.IsNullOrWhiteSpace(model.EstimatedTime) ? null : model.EstimatedTime.Trim();
        var sectorKey = DeliverySectorRules.Key(model.Sector);
        var existingSectors = await db.DeliveryZones.AsNoTracking()
            .Where(zone => zone.Id != model.Id && zone.Province == ProvinceName && zone.City == model.City)
            .Select(zone => zone.Sector).ToListAsync(ct);
        if (existingSectors.Any(sector => DeliverySectorRules.Key(sector) == sectorKey))
        {
            ModelState.AddModelError(nameof(model.Sector), "Ya existe una zona para este sector y ciudad. Edita la zona existente.");
            return View(model);
        }

        var zone = model.Id == 0 ? new DeliveryZone() : await db.DeliveryZones.SingleOrDefaultAsync(item => item.Id == model.Id, ct);
        if (zone is null) return NotFound();
        var creating = model.Id == 0;
        var before = creating ? null : AuditSnapshot(zone);
        zone.Province = ProvinceName;
        zone.City = model.City;
        zone.Sector = model.Sector;
        zone.Cost = model.Cost;
        zone.IsFree = model.IsFree;
        zone.RequiresConfirmation = model.RequiresConfirmation;
        zone.EstimatedTime = model.EstimatedTime;
        zone.IsActive = model.IsActive;
        zone.SortOrder = model.SortOrder;
        if (creating) db.DeliveryZones.Add(zone);
        else zone.UpdatedAt = DateTime.UtcNow;

        try
        {
            // El logger guarda la zona pendiente y el registro de auditoría en la misma operación SQL.
            // El identificador de una zona nueva todavía no existe; sus datos quedan en la copia auditada.
            await audit.RecordAsync(creating ? "ShippingZone.Created" : "ShippingZone.Updated", "DeliveryZone",
                creating ? null : zone.Id.ToString(CultureInfo.InvariantCulture), "200", before,
                AuditSnapshot(zone), ct);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(nameof(model.Sector), "Ya existe una zona para este sector y ciudad. Actualiza la lista e inténtalo de nuevo.");
            return View(model);
        }

        TempData["Success"] = creating ? "Zona de entrega creada." : "Zona de entrega actualizada. Los pedidos anteriores conservan su tarifa.";
        return RedirectToAction(nameof(Index));
    }

    private static string AuditSnapshot(DeliveryZone zone) => JsonSerializer.Serialize(new
    {
        zone.Province, zone.City, zone.Sector, zone.Cost, zone.IsFree, zone.RequiresConfirmation,
        zone.EstimatedTime, zone.IsActive, zone.SortOrder
    });
}

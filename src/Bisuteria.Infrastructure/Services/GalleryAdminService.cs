using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.Validators;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Infrastructure.Services;

/// <summary>
/// Administración de la galería de imágenes del sitio.
/// </summary>
public class GalleryAdminService : IGalleryAdminService
{
    private readonly AppDbContext _db;

    public GalleryAdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<GalleryImageDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.GalleryImages
            .AsNoTracking()
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GalleryImageDto
            {
                Id = g.Id,
                ImageUrl = g.ImageUrl,
                Title = g.Title,
                Description = g.Description,
                Category = g.Category,
                IsFeatured = g.IsFeatured
            })
            .ToListAsync(ct);
    }

    public async Task<GalleryImageDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.GalleryImages
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new GalleryImageDto
            {
                Id = g.Id,
                ImageUrl = g.ImageUrl,
                Title = g.Title,
                Description = g.Description,
                Category = g.Category,
                IsFeatured = g.IsFeatured
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> CreateAsync(GalleryImageDto dto, CancellationToken ct = default)
    {
        InputValidation.Check(new GalleryImageValidator(), dto);
        if (string.IsNullOrWhiteSpace(dto.ImageUrl)) throw new Bisuteria.Application.Services.BusinessRuleException("Indica una imagen válida.");
        var entity = new GalleryImage
        {
            ImageUrl = dto.ImageUrl,
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            IsFeatured = dto.IsFeatured
        };
        _db.GalleryImages.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(GalleryImageDto dto, CancellationToken ct = default)
    {
        InputValidation.Check(new GalleryImageValidator(), dto);
        if (string.IsNullOrWhiteSpace(dto.ImageUrl)) throw new Bisuteria.Application.Services.BusinessRuleException("Indica una imagen válida.");
        var entity = await _db.GalleryImages.FirstOrDefaultAsync(g => g.Id == dto.Id, ct)
            ?? throw new Bisuteria.Application.Services.BusinessRuleException("La imagen no existe.");

        entity.ImageUrl = dto.ImageUrl;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.IsFeatured = dto.IsFeatured;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.GalleryImages.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entity is not null)
        {
            _db.GalleryImages.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}

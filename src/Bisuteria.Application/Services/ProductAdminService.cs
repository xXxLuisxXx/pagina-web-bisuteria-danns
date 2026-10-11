using Bisuteria.Application.Validators;
using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de gestión del catálogo (CRUD de productos y categorías).
/// </summary>
public class ProductAdminService : IProductAdminService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductAdminService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductInputDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var product = await _unitOfWork.Repository<Product>().Query().Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? null : EntityMappings.ToInput(product);
    }

    public Task<int> SaveAsync(ProductInputDto dto, IEnumerable<string> newImageUrls, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => SaveCoreAsync(dto, newImageUrls, ct), ct);

    private async Task<int> SaveCoreAsync(ProductInputDto dto, IEnumerable<string> newImageUrls, CancellationToken ct)
    {
        InputValidation.Check(new ProductInputValidator(), dto);
        if (!await _unitOfWork.Repository<Category>().AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new Bisuteria.Application.Services.BusinessRuleException("La categoría no existe.");
        var repo = _unitOfWork.Repository<Product>();
        dto.Sku = string.IsNullOrWhiteSpace(dto.Sku) ? null : dto.Sku.Trim();
        if (dto.Sku is not null && await repo.AnyAsync(p => p.Sku == dto.Sku && p.Id != dto.Id, ct))
            throw new Bisuteria.Application.Services.BusinessRuleException("El SKU ya pertenece a otro producto.");
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (dto.Id == 0 && await repo.AnyAsync(p => p.Slug == slug, ct))
            throw new Bisuteria.Application.Services.BusinessRuleException("Ya existe un producto con ese nombre.");
        var imageRepo = _unitOfWork.Repository<ProductImage>();
        Product product;
        var isNew = dto.Id == 0;

        if (isNew)
        {
            product = new Product();
            await repo.AddAsync(product, ct);
        }
        else
        {
            product = (await repo.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == dto.Id, ct))!;
            if (product is null)
                throw new Bisuteria.Application.Services.BusinessRuleException("El producto no existe.");
        }

        if (!isNew && (!dto.OriginalStock.HasValue || dto.OriginalStock.Value != product.Stock))
            throw new Bisuteria.Application.Services.BusinessRuleException("El inventario cambió o falta su versión. Recarga el producto antes de guardar.");
        var stockDelta = dto.Stock - product.Stock;

        product.Name = dto.Name.Trim();
        product.Slug = isNew || product.Slug == string.Empty
            ? SlugHelper.GenerateSlug(dto.Name)
            : product.Slug;
        product.Description = dto.Description ?? string.Empty;
        product.Price = dto.Price;
        product.ComparePrice = dto.ComparePrice;
        product.Material = dto.Material;
        product.Colors = dto.Colors;
        product.Size = dto.Size;
        product.Sku = dto.Sku;
        product.Stock = dto.Stock;
        product.MinStock = dto.MinStock;
        product.IsActive = dto.IsActive;
        product.IsFeatured = dto.IsFeatured;
        product.IsNew = dto.IsNew;
        product.IsCustomizable = dto.IsCustomizable;
        product.CategoryId = dto.CategoryId;
        product.UpdatedAt = DateTime.UtcNow;

        foreach (var image in product.Images.Where(i => dto.RemoveImageIds.Contains(i.Id)).ToList())
        {
            product.Images.Remove(image);
            imageRepo.Remove(image);
        }
        if (product.Images.Count > 0 && !product.Images.Any(i => i.IsPrimary))
            product.Images.OrderBy(i => i.DisplayOrder).First().IsPrimary = true;
        // Agregar nuevas imágenes.
        foreach (var url in newImageUrls.SelectMany(x => x.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Select(x => x.Trim()).Distinct())
        {
            if (string.IsNullOrWhiteSpace(url))
                continue;
            if (!InputValidation.IsImageUrl(url))
                throw new Bisuteria.Application.Services.BusinessRuleException("Sube una imagen o usa una ruta local que empiece por /img/.");
            var image = new ProductImage
            {
                ImageUrl = url,
                IsPrimary = product.Images.Count == 0,
                DisplayOrder = product.Images.Count
            };

            if (isNew)
            {
                // Vía navegación: EF asigna el FK real al guardar (el Id aún es temporal).
                product.Images.Add(image);
            }
            else
            {
                image.ProductId = product.Id;
                await imageRepo.AddAsync(image, ct);
            }
        }

        if (!isNew)
            repo.Update(product);
        await _unitOfWork.SaveChangesAsync(ct);
        if (stockDelta != 0)
        {
            await _unitOfWork.Repository<StockMovement>().AddAsync(new StockMovement
            {
                ProductId = product.Id,
                Type = stockDelta > 0 ? StockMovementType.In : StockMovementType.Out,
                Quantity = Math.Abs(stockDelta), StockAfter = product.Stock,
                Reason = isNew ? "Inventario inicial" : "Ajuste desde edición del producto"
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        return product.Id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Product>();
        var product = await repo.GetByIdAsync(id, ct);
        if (product is null)
            return false;

        // Marcar como inactivo en lugar de borrado físico si tiene historial.
        if (await _unitOfWork.Repository<OrderItem>().AnyAsync(i => i.ProductId == id, ct) ||
            await _unitOfWork.Repository<StockMovement>().AnyAsync(i => i.ProductId == id, ct))
        {
            product.IsActive = false;
            repo.Update(product);
        }
        else
        {
            repo.Remove(product);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<CategoryDto>> GetCategoryOptionsAsync(CancellationToken ct = default)
        => await _unitOfWork.Repository<Category>().Query()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                DisplayOrder = c.DisplayOrder
            })
            .ToListAsync(ct);

    public async Task<int> SaveCategoryAsync(CategoryInputDto dto, CancellationToken ct = default)
    {
        InputValidation.Check(new CategoryInputValidator(), dto);
        var repo = _unitOfWork.Repository<Category>();
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (dto.Id == 0 && await repo.AnyAsync(c => c.Slug == slug, ct))
            throw new Bisuteria.Application.Services.BusinessRuleException("Ya existe una categoría con ese nombre.");
        Category category;
        var isNew = dto.Id == 0;

        if (isNew)
        {
            category = new Category();
            await repo.AddAsync(category, ct);
        }
        else
        {
            category = (await repo.GetByIdAsync(dto.Id, ct))!;
            if (category is null)
                throw new Bisuteria.Application.Services.BusinessRuleException("La categoría no existe.");
        }

        category.Name = dto.Name.Trim();
        category.Slug = isNew ? SlugHelper.GenerateSlug(dto.Name) : category.Slug;
        category.Description = dto.Description;
        category.IsActive = dto.IsActive;
        category.DisplayOrder = dto.DisplayOrder;
        category.UpdatedAt = DateTime.UtcNow;

        repo.Update(category);
        await _unitOfWork.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Category>();
        var category = await repo.GetByIdAsync(id, ct);
        if (category is null)
            return false;

        // No eliminar si tiene productos asociados.
        if (await _unitOfWork.Repository<Product>().AnyAsync(p => p.CategoryId == id, ct))
            return false;

        repo.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

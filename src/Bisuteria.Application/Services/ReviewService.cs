using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio de reseñas.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ReviewDto>> GetApprovedAsync(int? productId = null, CancellationToken ct = default)
    {
        var q = _unitOfWork.Repository<Review>().Query()
            .Where(r => r.IsApproved);

        if (productId.HasValue)
            q = q.Where(r => r.ProductId == productId.Value);

        return await q
            .OrderByDescending(r => r.ReviewDate)
            .Select(EntityMappings.ReviewProjection)
            .ToListAsync(ct);
    }

    public Task<bool> CreateAsync(CreateReviewDto dto, string? userId, string? customerName, CancellationToken ct = default)
        => _unitOfWork.InTransactionAsync(() => CreateCoreAsync(dto, userId, customerName, ct), ct);

    private async Task<bool> CreateCoreAsync(CreateReviewDto dto, string? userId, string? customerName, CancellationToken ct)
    {
        Bisuteria.Application.Validators.InputValidation.Check(new Bisuteria.Application.Validators.CreateReviewValidator(), dto);
        if (string.IsNullOrWhiteSpace(userId) || !await _unitOfWork.Repository<Product>().AnyAsync(p => p.Id == dto.ProductId && p.IsActive && p.Category != null && p.Category.IsActive, ct)) return false;
        var repo = _unitOfWork.Repository<Review>();
        var alreadyReviewed = await repo.AnyAsync(
            r => r.ProductId == dto.ProductId && r.UserId == userId, ct);

        // Permitir una sola reseña por usuario y producto.
        if (alreadyReviewed)
            return false;

        await repo.AddAsync(new Review
        {
            UserId = userId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Cliente" : customerName,
            ProductId = dto.ProductId,
            Rating = Math.Clamp(dto.Rating, 1, 5),
            Title = dto.Title,
            Comment = dto.Comment,
            ReviewDate = DateTime.UtcNow,
            IsApproved = false
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ApproveAsync(int reviewId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Review>();
        var review = await repo.GetByIdAsync(reviewId, ct);
        if (review is null)
            return false;

        review.IsApproved = true;
        repo.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int reviewId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Review>();
        var review = await repo.GetByIdAsync(reviewId, ct);
        if (review is null)
            return false;

        repo.Remove(review);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CountPendingAsync(CancellationToken ct = default)
        => await _unitOfWork.Repository<Review>().CountAsync(r => !r.IsApproved, ct);

    public async Task<List<ReviewAdminDto>> GetAdminListAsync(bool pendingOnly = false, CancellationToken ct = default)
    {
        var q = _unitOfWork.Repository<Review>().Query()
            .Include(r => r.Product)
            .AsQueryable();

        if (pendingOnly)
            q = q.Where(r => !r.IsApproved);

        return await q
            .OrderByDescending(r => r.ReviewDate)
            .Select(r => new ReviewAdminDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductName = r.Product != null ? r.Product.Name : string.Empty,
                ProductImageUrl = r.Product != null ? r.Product.MainImageUrl ?? string.Empty : string.Empty,
                CustomerName = r.CustomerName,
                Rating = r.Rating,
                Title = r.Title,
                Comment = r.Comment,
                IsApproved = r.IsApproved,
                ReviewDate = r.ReviewDate
            })
            .ToListAsync(ct);
    }
}

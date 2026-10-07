using Bisuteria.Application.DTOs.Blog;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Servicio del blog de consejos y tendencias.
/// </summary>
public interface IBlogService
{
    Task<BlogPostInputDto?> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    Task<List<BlogPostDto>> GetPublishedAsync(int count = 6, CancellationToken cancellationToken = default);

    Task<BlogPostDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PaginatedBlogResult> GetPagedAsync(int page, int pageSize, bool publishedOnly = true, string? search = null, CancellationToken cancellationToken = default);

    Task<int> SaveAsync(BlogPostInputDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task IncrementViewsAsync(int id, CancellationToken cancellationToken = default);
}

public record PaginatedBlogResult(
    IReadOnlyList<BlogPostDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

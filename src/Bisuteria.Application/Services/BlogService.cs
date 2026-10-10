using Bisuteria.Application.Validators;
using Bisuteria.Application.Mappings;
using Bisuteria.Application.DTOs.Blog;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Application.Services;

/// <summary>
/// Implementación del servicio del blog.
/// </summary>
public class BlogService : IBlogService
{
    private readonly IUnitOfWork _unitOfWork;

    public BlogService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<BlogPostInputDto?> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var post = await _unitOfWork.Repository<BlogPost>().GetByIdAsync(id, cancellationToken);
        return post is null ? null : new BlogPostInputDto { Id = post.Id, Title = post.Title, Excerpt = post.Excerpt, Content = post.Content, Author = post.Author, Tags = post.Tags, IsPublished = post.IsPublished };
    }

    public async Task<List<BlogPostDto>> GetPublishedAsync(int count = 6, CancellationToken ct = default)
        => await _unitOfWork.Repository<BlogPost>().Query()
            .Where(b => b.IsPublished)
            .OrderByDescending(b => b.PublishedAt)
            .Take(count)
            .Select(EntityMappings.BlogProjection)
            .ToListAsync(ct);

    public async Task<BlogPostDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _unitOfWork.Repository<BlogPost>().Query()
            .Where(b => b.Slug == slug && b.IsPublished)
            .Select(EntityMappings.BlogProjection)
            .FirstOrDefaultAsync(ct);

    public async Task<PaginatedBlogResult> GetPagedAsync(int page, int pageSize, bool publishedOnly = true, string? search = null, CancellationToken ct = default)
    {
        var q = _unitOfWork.Repository<BlogPost>().Query();

        if (publishedOnly)
            q = q.Where(b => b.IsPublished);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(b => b.Title.Contains(term) || (b.Tags != null && b.Tags.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var items = await q
            .OrderByDescending(b => b.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(EntityMappings.BlogProjection)
            .ToListAsync(ct);

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new PaginatedBlogResult(items, total, page, pageSize, totalPages);
    }

    public async Task<int> SaveAsync(BlogPostInputDto dto, CancellationToken ct = default)
    {
        InputValidation.Check(new BlogPostValidator(), dto);
        var repo = _unitOfWork.Repository<BlogPost>();
        BlogPost entity;
        var isNew = dto.Id == 0;
        var slug = SlugHelper.GenerateSlug(dto.Title);
        if (isNew && await repo.AnyAsync(p => p.Slug == slug, ct))
            throw new Bisuteria.Application.Services.BusinessRuleException("Ya existe una publicación con ese título.");

        if (isNew)
        {
            entity = new BlogPost();
            await repo.AddAsync(entity, ct);
        }
        else
        {
            entity = (await repo.GetByIdAsync(dto.Id, ct))!;
            if (entity is null)
                throw new Bisuteria.Application.Services.BusinessRuleException("La publicación no existe.");
        }

        entity.Title = dto.Title.Trim();
        entity.Excerpt = dto.Excerpt.Trim();
        entity.Content = dto.Content;
        entity.Author = string.IsNullOrWhiteSpace(dto.Author) ? "Administración" : dto.Author.Trim();
        entity.Tags = dto.Tags;
        entity.IsPublished = dto.IsPublished;
        entity.Slug = isNew ? SlugHelper.GenerateSlug(dto.Title) : entity.Slug;
        entity.UpdatedAt = DateTime.UtcNow;
        if (dto.IsPublished && !entity.PublishedAt.HasValue)
            entity.PublishedAt = DateTime.UtcNow;
        if (!dto.IsPublished) entity.PublishedAt = null;

        repo.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<BlogPost>();
        var post = await repo.GetByIdAsync(id, ct);
        if (post is null)
            return false;

        repo.Remove(post);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task IncrementViewsAsync(int id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<BlogPost>();
        var post = await repo.GetByIdAsync(id, ct);
        if (post is null)
            return;
        post.Views++;
        repo.Update(post);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Utilidad para generar slugs amigables para URLs.
/// </summary>
public static class SlugHelper
{
    public static string GenerateSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N")[..8];

        var slug = value.Trim().ToLowerInvariant();
        var result = new System.Text.StringBuilder();
        foreach (var c in slug)
        {
            if (char.IsLetterOrDigit(c))
                result.Append(c);
            else if (c is ' ' or '-' or '_' or '.' or ',')
                result.Append('-');
        }

        var final = result.ToString();
        while (final.Contains("--"))
            final = final.Replace("--", "-");

        final = final.Trim('-');
        return string.IsNullOrEmpty(final) ? Guid.NewGuid().ToString("N")[..8] : final;
    }
}

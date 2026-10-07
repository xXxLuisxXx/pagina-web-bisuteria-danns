using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Publicación del blog (consejos sobre bisutería, regalos y tendencias).
/// </summary>
public class BlogPost : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? CoverImage { get; set; }

    public string? Author { get; set; }

    /// <summary>Etiquetas separadas por coma para búsqueda y filtros.</summary>
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime? PublishedAt { get; set; }

    public int Views { get; set; }
}

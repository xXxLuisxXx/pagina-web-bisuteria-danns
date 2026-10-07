using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>
/// Mensaje enviado a través del formulario de contacto.
/// </summary>
public class ContactMessage : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
}

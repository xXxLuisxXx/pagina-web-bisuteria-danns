using Microsoft.AspNetCore.Identity;

namespace Bisuteria.Infrastructure.Identity;

/// <summary>
/// Usuario de la aplicación extendido con los datos del cliente.
/// Hereda de IdentityUser para la autenticación de ASP.NET Identity.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string? PhotoUrl { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string? InitialPasswordHash { get; set; }
}

using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

/// <summary>Invitación de un solo uso para alta de propietario u operador.</summary>
public sealed class StaffInvitation : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string InvitedByUserId { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

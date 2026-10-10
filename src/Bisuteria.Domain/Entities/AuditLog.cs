using Bisuteria.Domain.Common;
namespace Bisuteria.Domain.Entities;

/// <summary>Metadatos de solicitudes administrativas; nunca almacena formularios ni secretos.</summary>
public sealed class AuditLog : BaseEntity
{
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Outcome { get; set; } = "";
}

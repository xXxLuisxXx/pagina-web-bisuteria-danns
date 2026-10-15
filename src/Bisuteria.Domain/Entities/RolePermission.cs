using Bisuteria.Domain.Common;

namespace Bisuteria.Domain.Entities;

public sealed class RolePermission : BaseEntity
{
    public string RoleName { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

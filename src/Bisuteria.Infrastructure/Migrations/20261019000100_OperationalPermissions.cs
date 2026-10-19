using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bisuteria.Infrastructure.Migrations;

// Migración exclusivamente de datos: no modifica el modelo ni el esquema existente.
[DbContext(typeof(AppDbContext))]
[Migration("20261019000100_OperationalPermissions")]
public sealed class OperationalPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO RolePermissions (RoleName, Permission, CreatedAt)
        SELECT r.RoleName, p.Permission, SYSUTCDATETIME()
        FROM (VALUES (N'Developer'), (N'Owner')) r(RoleName)
        CROSS JOIN (VALUES (N'Orders.SetShipping'), (N'Payments.ViewProof'), (N'Payments.ReviewProof'), (N'Customers.Manage')) p(Permission)
        WHERE NOT EXISTS (SELECT 1 FROM RolePermissions existing WHERE existing.RoleName = r.RoleName AND existing.Permission = p.Permission);
        IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleName = N'Developer' AND Permission = N'Customers.Reveal')
            INSERT INTO RolePermissions (RoleName, Permission, CreatedAt) VALUES (N'Developer', N'Customers.Reveal', SYSUTCDATETIME());
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM RolePermissions WHERE RoleName IN (N'Developer', N'Owner')
            AND Permission IN (N'Orders.SetShipping', N'Payments.ViewProof', N'Payments.ReviewProof', N'Customers.Manage', N'Customers.Reveal');
        """);
}

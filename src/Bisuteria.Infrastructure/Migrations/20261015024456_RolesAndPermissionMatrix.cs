using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bisuteria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RolesAndPermissionMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_Permission",
                table: "RolePermissions",
                columns: new[] { "RoleName", "Permission" },
                unique: true);

            // Roles base para instalaciones existentes. La inserción es idempotente
            // porque algunas instalaciones previas pudieron crearlos en el arranque.
            migrationBuilder.Sql("""
                DECLARE @roles TABLE (Name nvarchar(256) NOT NULL);
                INSERT INTO @roles (Name) VALUES (N'Developer'), (N'Owner'), (N'Operator'), (N'Customer');
                INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
                SELECT CONVERT(nvarchar(450), NEWID()), r.Name, UPPER(r.Name), CONVERT(nvarchar(36), NEWID())
                FROM @roles r WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles existing WHERE existing.NormalizedName = UPPER(r.Name));

                DECLARE @permissions TABLE (RoleName nvarchar(64), Permission nvarchar(128));
                INSERT INTO @permissions (RoleName, Permission) VALUES
                    (N'Developer',N'Dashboard.View'),(N'Developer',N'Orders.View'),(N'Developer',N'Orders.UpdateStatus'),(N'Developer',N'Payments.Update'),
                    (N'Developer',N'Products.Manage'),(N'Developer',N'Inventory.Adjust'),(N'Developer',N'Categories.Manage'),(N'Developer',N'Coupons.Manage'),
                    (N'Developer',N'Reviews.Moderate'),(N'Developer',N'Messages.View'),(N'Developer',N'Blog.Manage'),(N'Developer',N'Gallery.Manage'),
                    (N'Developer',N'Reports.View'),(N'Developer',N'Reports.Export'),(N'Developer',N'StoreSettings.Edit'),(N'Developer',N'Customers.ViewLimited'),
                    (N'Developer',N'Users.ManageOwner'),(N'Developer',N'Users.ManageStaff'),(N'Developer',N'Roles.ViewMatrix'),(N'Developer',N'Roles.EditMatrix'),
                    (N'Developer',N'Audit.View'),(N'Developer',N'System.View'),(N'Developer',N'Smtp.Configure'),(N'Developer',N'Security.Configure'),
                    (N'Developer',N'Admin.Search'),(N'Developer',N'Admin.Notifications'),
                    (N'Owner',N'Dashboard.View'),(N'Owner',N'Orders.View'),(N'Owner',N'Orders.UpdateStatus'),(N'Owner',N'Payments.Update'),
                    (N'Owner',N'Products.Manage'),(N'Owner',N'Inventory.Adjust'),(N'Owner',N'Categories.Manage'),(N'Owner',N'Coupons.Manage'),
                    (N'Owner',N'Reviews.Moderate'),(N'Owner',N'Messages.View'),(N'Owner',N'Blog.Manage'),(N'Owner',N'Gallery.Manage'),
                    (N'Owner',N'Reports.View'),(N'Owner',N'Reports.Export'),(N'Owner',N'StoreSettings.Edit'),(N'Owner',N'Customers.ViewLimited'),
                    (N'Owner',N'Users.ManageStaff'),(N'Owner',N'Admin.Search'),(N'Owner',N'Admin.Notifications'),
                    (N'Operator',N'Dashboard.View'),(N'Operator',N'Orders.View'),(N'Operator',N'Orders.UpdateStatus'),
                    (N'Operator',N'Inventory.Adjust'),(N'Operator',N'Admin.Search'),(N'Operator',N'Admin.Notifications');

                INSERT INTO RolePermissions (RoleName, Permission, CreatedAt)
                SELECT p.RoleName, p.Permission, SYSUTCDATETIME()
                FROM @permissions p
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions existing WHERE existing.RoleName = p.RoleName);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");
        }
    }
}

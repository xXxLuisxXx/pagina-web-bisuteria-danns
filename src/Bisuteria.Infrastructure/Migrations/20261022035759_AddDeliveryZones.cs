using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bisuteria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliverySector",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryZoneId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryZoneName",
                table: "Orders",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Province = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    RequiresConfirmation = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedTime = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryZoneId",
                table: "Orders",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_IsActive_SortOrder_City",
                table: "DeliveryZones",
                columns: new[] { "IsActive", "SortOrder", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_Province_City_Sector",
                table: "DeliveryZones",
                columns: new[] { "Province", "City", "Sector" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryZones_DeliveryZoneId",
                table: "Orders",
                column: "DeliveryZoneId",
                principalTable: "DeliveryZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Las nuevas opciones no fijan tarifas ni plazos comerciales.
            migrationBuilder.Sql("""
                INSERT INTO DeliveryZones (Province, City, Sector, Cost, IsFree, RequiresConfirmation, IsActive, SortOrder, CreatedAt)
                VALUES (N'Manabí', N'Manta', N'Otro sector (por confirmar)', 0, 0, 1, 1, 1, SYSUTCDATETIME()),
                       (N'Manabí', N'Jaramijó', N'Otro sector (por confirmar)', 0, 0, 1, 1, 2, SYSUTCDATETIME());

                INSERT INTO RolePermissions (RoleName, Permission, CreatedAt)
                SELECT roles.RoleName, N'Shipping.Manage', SYSUTCDATETIME()
                FROM (VALUES (N'Developer'), (N'Owner')) roles(RoleName)
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions existing WHERE existing.RoleName = roles.RoleName AND existing.Permission = N'Shipping.Manage');

                IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = N'FreeShippingThresholdEnabled')
                    INSERT INTO SiteSettings ([Key], Value, [Group], CreatedAt) VALUES (N'FreeShippingThresholdEnabled', N'false', N'Compras', SYSUTCDATETIME());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Esta migración conserva el historial de tarifas; requiere una reversión aditiva revisada.");
        }
    }
}

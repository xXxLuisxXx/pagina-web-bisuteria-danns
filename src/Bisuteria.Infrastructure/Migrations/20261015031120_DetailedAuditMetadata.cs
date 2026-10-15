using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bisuteria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DetailedAuditMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                table: "AuditLog",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AfterJson",
                table: "AuditLog",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeforeJson",
                table: "AuditLog",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "AuditLog",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "AuditLog",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteIp",
                table: "AuditLog",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Action_CreatedAt",
                table: "AuditLog",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorId_CreatedAt",
                table: "AuditLog",
                columns: new[] { "ActorId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLog_Action_CreatedAt",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_ActorId_CreatedAt",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "AfterJson",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "BeforeJson",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "RemoteIp",
                table: "AuditLog");
        }
    }
}

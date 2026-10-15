using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bisuteria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SqlDistributedSessionStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributedCache",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(449)", maxLength: 449, nullable: false),
                    Value = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ExpiresAtTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SlidingExpirationInSeconds = table.Column<long>(type: "bigint", nullable: true),
                    AbsoluteExpiration = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "Index_ExpiresAtTime",
                table: "DistributedCache",
                column: "ExpiresAtTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributedCache");
        }
    }
}

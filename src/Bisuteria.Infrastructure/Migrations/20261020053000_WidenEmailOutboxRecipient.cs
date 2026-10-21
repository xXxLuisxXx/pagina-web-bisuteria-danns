using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bisuteria.Infrastructure.Migrations
{
    /// <summary>Amplía destinatarios de correo sin modificar los datos existentes.</summary>
    public partial class WidenEmailOutboxRecipient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Recipient", table: "EmailOutbox", type: "nvarchar(254)", maxLength: 254,
                nullable: false, oldClrType: typeof(string), oldType: "nvarchar(200)", oldMaxLength: 200);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Impide truncar destinatarios si se solicita una reversión del esquema.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM EmailOutbox WHERE LEN(Recipient) > 200) THROW 51000, 'Hay destinatarios largos; revisa la reversion antes de reducir la columna.', 1;");
            migrationBuilder.AlterColumn<string>(
                name: "Recipient", table: "EmailOutbox", type: "nvarchar(200)", maxLength: 200,
                nullable: false, oldClrType: typeof(string), oldType: "nvarchar(254)", oldMaxLength: 254);
        }
    }
}

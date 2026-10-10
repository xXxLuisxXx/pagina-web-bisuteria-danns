using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bisuteria.Infrastructure.Migrations;

public partial class CheckoutIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT Sku FROM Products WHERE Sku IS NOT NULL GROUP BY Sku HAVING COUNT(*) > 1) THROW 51000, 'Existen SKU duplicados. Revisar antes de aplicar la migración.', 1;");
        migrationBuilder.CreateIndex("UX_Products_Sku", "Products", "Sku", unique: true, filter: "[Sku] IS NOT NULL");
        migrationBuilder.AddColumn<Guid>("CheckoutToken", "Orders", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("CouponCode", "Orders", type: "nvarchar(60)", maxLength: 60, nullable: true);
        migrationBuilder.CreateIndex("IX_Orders_CheckoutToken", "Orders", "CheckoutToken", unique: true, filter: "[CheckoutToken] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("UX_Products_Sku", "Products");
        migrationBuilder.DropIndex("IX_Orders_CheckoutToken", "Orders");
        migrationBuilder.DropColumn("CheckoutToken", "Orders");
        migrationBuilder.DropColumn("CouponCode", "Orders");
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WildBerriesAnalyzer.Data.Migrations
{
    [DbContext(typeof(WbDataBase))]
    [Migration("20260811220000_AddProductMarketType")]
    public partial class AddProductMarketType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MarketType",
                table: "Products",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_Products_IdInMarket",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_MarketType_IdInMarket",
                table: "Products",
                columns: new[] { "MarketType", "IdInMarket" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_MarketType",
                table: "Products",
                column: "MarketType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_MarketType",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_MarketType_IdInMarket",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MarketType",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IdInMarket",
                table: "Products",
                column: "IdInMarket",
                unique: true);
        }
    }
}

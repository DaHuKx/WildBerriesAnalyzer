using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Data.Migrations
{
    [DbContext(typeof(WbDataBase))]
    [Migration("20260815120000_AddCategoryMarketMeta")]
    public partial class AddCategoryMarketMeta : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MarketCategoryId",
                table: "Categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarketType",
                table: "Categories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_MarketType_MarketCategoryId",
                table: "Categories",
                columns: new[] { "MarketType", "MarketCategoryId" },
                unique: true,
                filter: "\"MarketCategoryId\" IS NOT NULL AND \"MarketType\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_MarketType_MarketCategoryId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MarketCategoryId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MarketType",
                table: "Categories");
        }
    }
}

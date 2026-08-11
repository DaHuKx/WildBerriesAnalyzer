using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WildBerriesAnalyzer.Data.Migrations
{
    [DbContext(typeof(WbDataBase))]
    [Migration("20260811230000_AddFilterMarketTypes")]
    public partial class AddFilterMarketTypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "MarketTypes",
                table: "Filters",
                type: "text[]",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketTypes",
                table: "Filters");
        }
    }
}

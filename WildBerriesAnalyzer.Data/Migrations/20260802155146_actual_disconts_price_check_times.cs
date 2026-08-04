using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WildBerriesAnalyzer.Data.Migrations
{
    public partial class actual_disconts_price_check_times : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPriceCheckTime",
                table: "ActualDisconts",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReferencePriceCheckTime",
                table: "ActualDisconts",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPriceCheckTime",
                table: "ActualDisconts");

            migrationBuilder.DropColumn(
                name: "ReferencePriceCheckTime",
                table: "ActualDisconts");
        }
    }
}

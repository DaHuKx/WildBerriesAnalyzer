using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WildBerriesAnalyzer.Data.Migrations
{
    public partial class strategies_added_to_filters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferencePriceStrartegy",
                table: "Filters");

            migrationBuilder.AddColumn<string[]>(
                name: "ReferencePriceStrartegies",
                table: "Filters",
                type: "text[]",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferencePriceStrartegies",
                table: "Filters");

            migrationBuilder.AddColumn<int>(
                name: "ReferencePriceStrartegy",
                table: "Filters",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}

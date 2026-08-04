using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace WildBerriesAnalyzer.Data.Migrations
{
    public partial class actual_disconts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActualDisconts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    ProductId = table.Column<int>(nullable: false),
                    PriceUpdateJobId = table.Column<int>(nullable: true),
                    ReferencePriceStrategy = table.Column<int>(nullable: false),
                    DiscontPercent = table.Column<decimal>(nullable: false),
                    CurrentPrice = table.Column<decimal>(nullable: false),
                    ReferencePrice = table.Column<decimal>(nullable: true),
                    CalculatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActualDisconts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActualDisconts_PriceUpdateJobs_PriceUpdateJobId",
                        column: x => x.PriceUpdateJobId,
                        principalTable: "PriceUpdateJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActualDisconts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActualDisconts_DiscontPercent",
                table: "ActualDisconts",
                column: "DiscontPercent");

            migrationBuilder.CreateIndex(
                name: "IX_ActualDisconts_PriceUpdateJobId",
                table: "ActualDisconts",
                column: "PriceUpdateJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ActualDisconts_Product_Strategy",
                table: "ActualDisconts",
                columns: new[] { "ProductId", "ReferencePriceStrategy" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActualDisconts");
        }
    }
}

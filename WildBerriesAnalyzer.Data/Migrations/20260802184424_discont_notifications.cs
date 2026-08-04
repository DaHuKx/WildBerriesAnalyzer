using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace WildBerriesAnalyzer.Data.Migrations
{
    public partial class discont_notifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscontNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    UserId = table.Column<int>(nullable: false),
                    ProductId = table.Column<int>(nullable: false),
                    ReferencePriceStrategy = table.Column<int>(nullable: false),
                    DiscontPercent = table.Column<decimal>(nullable: false),
                    CurrentPrice = table.Column<decimal>(nullable: false),
                    SentAt = table.Column<DateTime>(nullable: false),
                    PriceUpdateJobId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscontNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscontNotifications_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscontNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscontNotifications_ProductId",
                table: "DiscontNotifications",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscontNotifications_User_Product_Strategy",
                table: "DiscontNotifications",
                columns: new[] { "UserId", "ProductId", "ReferencePriceStrategy" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscontNotifications");
        }
    }
}

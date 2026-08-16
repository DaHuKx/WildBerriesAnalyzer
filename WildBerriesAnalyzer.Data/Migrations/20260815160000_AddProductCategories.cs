using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace WildBerriesAnalyzer.Data.Migrations
{
    [DbContext(typeof(WbDataBase))]
    [Migration("20260815160000_AddProductCategories")]
    public partial class AddProductCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(nullable: false),
                    CategoryId = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductCategories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_CategoryId",
                table: "ProductCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_ProductId_CategoryId",
                table: "ProductCategories",
                columns: new[] { "ProductId", "CategoryId" },
                unique: true);

            // Бэкфилл: существующий Product.CategoryId → ProductCategories
            migrationBuilder.Sql(@"
INSERT INTO ""ProductCategories"" (""ProductId"", ""CategoryId"", ""CreatedAt"")
SELECT p.""Id"", p.""CategoryId"", NOW() AT TIME ZONE 'utc'
FROM ""Products"" p
WHERE p.""CategoryId"" IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM ""ProductCategories"" pc
    WHERE pc.""ProductId"" = p.""Id"" AND pc.""CategoryId"" = p.""CategoryId""
  );
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProductCategories");
        }
    }
}

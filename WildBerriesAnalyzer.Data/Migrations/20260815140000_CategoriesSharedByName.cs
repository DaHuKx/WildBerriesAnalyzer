using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WildBerriesAnalyzer.Data.Migrations
{
    [DbContext(typeof(WbDataBase))]
    [Migration("20260815140000_CategoriesSharedByName")]
    public partial class CategoriesSharedByName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS — на случай повторного запуска после частично упавшей миграции.
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_Categories_MarketType_MarketCategoryId"";");

            // Таблицы EF в PascalCase — нужны кавычки, иначе Postgres ищет products.
            migrationBuilder.Sql(@"
UPDATE ""Products"" SET ""CategoryId"" = NULL
WHERE ""CategoryId"" IN (
  SELECT ""Id"" FROM ""Categories""
  WHERE ""Name"" ~ '^Категория [0-9]+$'
);

DELETE FROM ""CategoryFilters""
WHERE ""CategoryId"" IN (
  SELECT ""Id"" FROM ""Categories""
  WHERE ""Name"" ~ '^Категория [0-9]+$'
);

DELETE FROM ""Categories""
WHERE ""Name"" ~ '^Категория [0-9]+$';

UPDATE ""Categories""
SET ""MarketType"" = NULL,
    ""MarketCategoryId"" = NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Categories_MarketType_MarketCategoryId",
                table: "Categories",
                columns: new[] { "MarketType", "MarketCategoryId" },
                unique: true,
                filter: "\"MarketCategoryId\" IS NOT NULL AND \"MarketType\" IS NOT NULL");
        }
    }
}

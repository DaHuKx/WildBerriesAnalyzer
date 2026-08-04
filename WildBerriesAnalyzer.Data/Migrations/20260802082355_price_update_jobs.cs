using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace WildBerriesAnalyzer.Data.Migrations
{
    public partial class price_update_jobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceUpdateJobs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    Status = table.Column<int>(nullable: false),
                    CompletedAt = table.Column<DateTime>(nullable: false),
                    ProductsCount = table.Column<int>(nullable: false),
                    PricesSavedCount = table.Column<int>(nullable: false),
                    LockedAt = table.Column<DateTime>(nullable: true),
                    LockedBy = table.Column<string>(maxLength: 200, nullable: true),
                    ProcessedAt = table.Column<DateTime>(nullable: true),
                    AttemptCount = table.Column<int>(nullable: false),
                    LastError = table.Column<string>(maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceUpdateJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdateJobs_LockedAt",
                table: "PriceUpdateJobs",
                column: "LockedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdateJobs_Status_CompletedAt",
                table: "PriceUpdateJobs",
                columns: new[] { "Status", "CompletedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceUpdateJobs");
        }
    }
}

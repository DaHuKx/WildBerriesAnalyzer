using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace WildBerriesAnalyzer.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    VkId = table.Column<string>(maxLength: 64, nullable: true),
                    Login = table.Column<string>(maxLength: 100, nullable: true),
                    Password = table.Column<string>(maxLength: 500, nullable: true),
                    AccessToken = table.Column<string>(maxLength: 2000, nullable: true),
                    RefreshToken = table.Column<string>(maxLength: 2000, nullable: true),
                    BotPlace = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IdInMarket = table.Column<long>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Brand = table.Column<string>(nullable: false),
                    CategoryId = table.Column<int>(nullable: true),
                    Rating = table.Column<double>(nullable: false),
                    ReviewRating = table.Column<double>(nullable: false),
                    FeedBacksCount = table.Column<int>(nullable: false),
                    ImageUrl = table.Column<string>(nullable: false),
                    Link = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Filters",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    UserId = table.Column<int>(nullable: false),
                    DiscontMinPercent = table.Column<int>(nullable: false),
                    MinReviewsCount = table.Column<int>(nullable: false),
                    MinRating = table.Column<float>(nullable: false),
                    ProductsFilterType = table.Column<int>(nullable: false),
                    ReferencePriceStrartegies = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Filters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Filters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VkLinkCodes",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    Code = table.Column<string>(maxLength: 16, nullable: false),
                    UserId = table.Column<int>(nullable: false),
                    ExpiresAt = table.Column<DateTime>(nullable: false),
                    UsedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VkLinkCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VkLinkCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    CurrentPriceCheckTime = table.Column<DateTime>(nullable: true),
                    ReferencePrice = table.Column<decimal>(nullable: true),
                    ReferencePriceCheckTime = table.Column<DateTime>(nullable: true),
                    ReferencePricePeriodFrom = table.Column<DateTime>(nullable: true),
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

            migrationBuilder.CreateTable(
                name: "PricesHistory",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    ProductId = table.Column<int>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    CheckTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricesHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricesHistory_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryFilters",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    FilterId = table.Column<int>(nullable: false),
                    CategoryId = table.Column<int>(nullable: false),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryFilters_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryFilters_Filters_FilterId",
                        column: x => x.FilterId,
                        principalTable: "Filters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FilterBags",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    FilterId = table.Column<int>(nullable: false),
                    ProductId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilterBags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilterBags_Filters_FilterId",
                        column: x => x.FilterId,
                        principalTable: "Filters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FilterBags_Products_ProductId",
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

            migrationBuilder.CreateIndex(
                name: "IX_CategoryFilters_CategoryId",
                table: "CategoryFilters",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryFilters_FilterId",
                table: "CategoryFilters",
                column: "FilterId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscontNotifications_ProductId",
                table: "DiscontNotifications",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscontNotifications_User_Product_Strategy",
                table: "DiscontNotifications",
                columns: new[] { "UserId", "ProductId", "ReferencePriceStrategy" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilterBags_FilterId",
                table: "FilterBags",
                column: "FilterId");

            migrationBuilder.CreateIndex(
                name: "IX_FilterBags_ProductId",
                table: "FilterBags",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Filters_UserId",
                table: "Filters",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricesHistory_ProductId",
                table: "PricesHistory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdateJobs_LockedAt",
                table: "PriceUpdateJobs",
                column: "LockedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdateJobs_Status_CompletedAt",
                table: "PriceUpdateJobs",
                columns: new[] { "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IdInMarket",
                table: "Products",
                column: "IdInMarket",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccessToken",
                table: "Users",
                column: "AccessToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshToken",
                table: "Users",
                column: "RefreshToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_VkId",
                table: "Users",
                column: "VkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VkLinkCodes_Code",
                table: "VkLinkCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VkLinkCodes_UserId",
                table: "VkLinkCodes",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActualDisconts");

            migrationBuilder.DropTable(
                name: "CategoryFilters");

            migrationBuilder.DropTable(
                name: "DiscontNotifications");

            migrationBuilder.DropTable(
                name: "FilterBags");

            migrationBuilder.DropTable(
                name: "PricesHistory");

            migrationBuilder.DropTable(
                name: "VkLinkCodes");

            migrationBuilder.DropTable(
                name: "PriceUpdateJobs");

            migrationBuilder.DropTable(
                name: "Filters");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}

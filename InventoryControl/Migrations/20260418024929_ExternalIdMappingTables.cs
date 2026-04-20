using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryControl.Migrations
{
    /// <inheritdoc />
    public partial class ExternalIdMappingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryExternalMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryExternalMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryExternalMappings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductExternalMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductExternalMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductExternalMappings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migrate existing ExternalId data to new mapping tables.
            // Use double-quoted identifiers so the SQL works on both SQLite and PostgreSQL.
            migrationBuilder.Sql(@"
                INSERT INTO ""ProductExternalMappings"" (""ProductId"", ""StoreName"", ""ExternalId"", ""Platform"")
                SELECT ""Id"", COALESCE(""ExternalIdSource"", 'unknown'), ""ExternalId"", COALESCE(""ExternalIdSource"", 'unknown')
                FROM ""Products""
                WHERE ""ExternalId"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""CategoryExternalMappings"" (""CategoryId"", ""StoreName"", ""ExternalId"", ""Platform"")
                SELECT ""Id"", COALESCE(""ExternalIdSource"", 'unknown'), ""ExternalId"", COALESCE(""ExternalIdSource"", 'unknown')
                FROM ""Categories""
                WHERE ""ExternalId"" IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExternalIdSource",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ExternalIdSource",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryExternalMappings_CategoryId_StoreName",
                table: "CategoryExternalMappings",
                columns: new[] { "CategoryId", "StoreName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryExternalMappings_StoreName_ExternalId",
                table: "CategoryExternalMappings",
                columns: new[] { "StoreName", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductExternalMappings_ProductId_StoreName",
                table: "ProductExternalMappings",
                columns: new[] { "ProductId", "StoreName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductExternalMappings_StoreName_ExternalId",
                table: "ProductExternalMappings",
                columns: new[] { "StoreName", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryExternalMappings");

            migrationBuilder.DropTable(
                name: "ProductExternalMappings");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Products",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdSource",
                table: "Products",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Categories",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdSource",
                table: "Categories",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryControl.Migrations
{
    /// <inheritdoc />
    public partial class ProductMappingConflictTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConflictDetails",
                table: "ProductExternalMappings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasConflict",
                table: "ProductExternalMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductExternalMappings_HasConflict",
                table: "ProductExternalMappings",
                column: "HasConflict");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductExternalMappings_HasConflict",
                table: "ProductExternalMappings");

            migrationBuilder.DropColumn(
                name: "ConflictDetails",
                table: "ProductExternalMappings");

            migrationBuilder.DropColumn(
                name: "HasConflict",
                table: "ProductExternalMappings");
        }
    }
}

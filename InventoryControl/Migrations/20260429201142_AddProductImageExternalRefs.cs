using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryControl.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageExternalRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages");

            migrationBuilder.AddColumn<string>(
                name: "ExternalImageId",
                table: "ProductImages",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUrl",
                table: "ProductImages",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_ExternalImageId",
                table: "ProductImages",
                columns: new[] { "ProductId", "ExternalImageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId_ExternalImageId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ExternalImageId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ExternalUrl",
                table: "ProductImages");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");
        }
    }
}

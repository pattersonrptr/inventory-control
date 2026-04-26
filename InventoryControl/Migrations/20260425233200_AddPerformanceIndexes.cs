using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryControl.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Date",
                table: "StockMovements",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CurrentStock_MinimumStock",
                table: "Products",
                columns: new[] { "CurrentStock", "MinimumStock" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_Date",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Products_CurrentStock_MinimumStock",
                table: "Products");
        }
    }
}

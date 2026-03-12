using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_Status",
                table: "tbl_Specimens",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tbl_Specimens_Status",
                table: "tbl_Specimens");
        }
    }
}

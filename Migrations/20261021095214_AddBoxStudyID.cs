using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class AddBoxStudyID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudyID",
                table: "tbl_Boxes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Boxes_StudyID",
                table: "tbl_Boxes",
                column: "StudyID");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_Boxes_tbl_Studies_StudyID",
                table: "tbl_Boxes",
                column: "StudyID",
                principalTable: "tbl_Studies",
                principalColumn: "StudyID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_Boxes_tbl_Studies_StudyID",
                table: "tbl_Boxes");

            migrationBuilder.DropIndex(
                name: "IX_tbl_Boxes_StudyID",
                table: "tbl_Boxes");

            migrationBuilder.DropColumn(
                name: "StudyID",
                table: "tbl_Boxes");
        }
    }
}

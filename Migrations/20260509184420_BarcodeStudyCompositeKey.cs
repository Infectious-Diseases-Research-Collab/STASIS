using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class BarcodeStudyCompositeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ""tbl_Specimens"" WHERE ""StudyID"" IS NULL) THEN
                        RAISE EXCEPTION 'tbl_Specimens has rows with StudyID IS NULL; assign a StudyID to every specimen before applying this migration.';
                    END IF;
                END $$;");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_Specimens_tbl_Studies_StudyID",
                table: "tbl_Specimens");

            migrationBuilder.DropIndex(
                name: "IX_tbl_Specimens_BarcodeID",
                table: "tbl_Specimens");

            migrationBuilder.AlterColumn<int>(
                name: "StudyID",
                table: "tbl_Specimens",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_StudyID_BarcodeID",
                table: "tbl_Specimens",
                columns: new[] { "StudyID", "BarcodeID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_Specimens_tbl_Studies_StudyID",
                table: "tbl_Specimens",
                column: "StudyID",
                principalTable: "tbl_Studies",
                principalColumn: "StudyID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_Specimens_tbl_Studies_StudyID",
                table: "tbl_Specimens");

            migrationBuilder.DropIndex(
                name: "IX_tbl_Specimens_StudyID_BarcodeID",
                table: "tbl_Specimens");

            migrationBuilder.AlterColumn<int>(
                name: "StudyID",
                table: "tbl_Specimens",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_BarcodeID",
                table: "tbl_Specimens",
                column: "BarcodeID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_Specimens_tbl_Studies_StudyID",
                table: "tbl_Specimens",
                column: "StudyID",
                principalTable: "tbl_Studies",
                principalColumn: "StudyID");
        }
    }
}

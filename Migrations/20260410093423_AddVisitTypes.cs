using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VisitTypeID",
                table: "tbl_Specimens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_VisitTypes",
                columns: table => new
                {
                    VisitTypeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_VisitTypes", x => x.VisitTypeID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_VisitTypeID",
                table: "tbl_Specimens",
                column: "VisitTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_VisitTypes_VisitTypeName",
                table: "tbl_VisitTypes",
                column: "VisitTypeName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_Specimens_tbl_VisitTypes_VisitTypeID",
                table: "tbl_Specimens",
                column: "VisitTypeID",
                principalTable: "tbl_VisitTypes",
                principalColumn: "VisitTypeID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_Specimens_tbl_VisitTypes_VisitTypeID",
                table: "tbl_Specimens");

            migrationBuilder.DropTable(
                name: "tbl_VisitTypes");

            migrationBuilder.DropIndex(
                name: "IX_tbl_Specimens_VisitTypeID",
                table: "tbl_Specimens");

            migrationBuilder.DropColumn(
                name: "VisitTypeID",
                table: "tbl_Specimens");
        }
    }
}

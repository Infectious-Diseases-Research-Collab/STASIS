using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class AddCompartmentHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_Racks_tbl_Freezers_FreezerID",
                table: "tbl_Racks");

            migrationBuilder.RenameColumn(
                name: "FreezerID",
                table: "tbl_Racks",
                newName: "CompartmentID");

            migrationBuilder.RenameIndex(
                name: "IX_tbl_Racks_FreezerID",
                table: "tbl_Racks",
                newName: "IX_tbl_Racks_CompartmentID");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "tbl_Racks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "tbl_Freezers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tbl_Compartments",
                columns: table => new
                {
                    CompartmentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompartmentName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FreezerID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Compartments", x => x.CompartmentID);
                    table.ForeignKey(
                        name: "FK_tbl_Compartments_tbl_Freezers_FreezerID",
                        column: x => x.FreezerID,
                        principalTable: "tbl_Freezers",
                        principalColumn: "FreezerID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Compartments_FreezerID_CompartmentName",
                table: "tbl_Compartments",
                columns: new[] { "FreezerID", "CompartmentName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_Racks_tbl_Compartments_CompartmentID",
                table: "tbl_Racks",
                column: "CompartmentID",
                principalTable: "tbl_Compartments",
                principalColumn: "CompartmentID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_Racks_tbl_Compartments_CompartmentID",
                table: "tbl_Racks");

            migrationBuilder.DropTable(
                name: "tbl_Compartments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "tbl_Racks");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "tbl_Freezers");

            migrationBuilder.RenameColumn(
                name: "CompartmentID",
                table: "tbl_Racks",
                newName: "FreezerID");

            migrationBuilder.RenameIndex(
                name: "IX_tbl_Racks_CompartmentID",
                table: "tbl_Racks",
                newName: "IX_tbl_Racks_FreezerID");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_Racks_tbl_Freezers_FreezerID",
                table: "tbl_Racks",
                column: "FreezerID",
                principalTable: "tbl_Freezers",
                principalColumn: "FreezerID");
        }
    }
}

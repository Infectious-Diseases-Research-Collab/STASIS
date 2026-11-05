using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStudyAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_UserStudyAccess",
                columns: table => new
                {
                    UserStudyAccessID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AspNetUserId = table.Column<string>(type: "text", nullable: false),
                    StudyID = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "text", nullable: false),
                    GrantedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_UserStudyAccess", x => x.UserStudyAccessID);
                    table.CheckConstraint("CK_UserStudyAccess_Role", "\"Role\" IN ('Viewer', 'Editor', 'StudyAdmin')");
                    table.ForeignKey(
                        name: "FK_tbl_UserStudyAccess_AspNetUsers_AspNetUserId",
                        column: x => x.AspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_UserStudyAccess_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_UserStudyAccess_tbl_Studies_StudyID",
                        column: x => x.StudyID,
                        principalTable: "tbl_Studies",
                        principalColumn: "StudyID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserStudyAccess_AspNetUserId_StudyID",
                table: "tbl_UserStudyAccess",
                columns: new[] { "AspNetUserId", "StudyID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserStudyAccess_GrantedByUserId",
                table: "tbl_UserStudyAccess",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserStudyAccess_StudyID",
                table: "tbl_UserStudyAccess",
                column: "StudyID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_UserStudyAccess");
        }
    }
}

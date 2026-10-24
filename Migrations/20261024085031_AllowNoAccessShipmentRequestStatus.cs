using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class AllowNoAccessShipmentRequestStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipmentRequests_Status",
                table: "tbl_ShipmentRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipmentRequests_Status",
                table: "tbl_ShipmentRequests",
                sql: "\"Status\" IN ('Pending', 'Staged', 'Shipped', 'Not Found', 'Previously Shipped', 'Discarded', 'Not Yet Received', 'No Access')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipmentRequests_Status",
                table: "tbl_ShipmentRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipmentRequests_Status",
                table: "tbl_ShipmentRequests",
                sql: "\"Status\" IN ('Pending', 'Staged', 'Shipped', 'Not Found', 'Previously Shipped', 'Discarded', 'Not Yet Received')");
        }
    }
}

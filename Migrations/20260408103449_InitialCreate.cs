using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Freezers",
                columns: table => new
                {
                    FreezerID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FreezerName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Temperature = table.Column<int>(type: "integer", nullable: true),
                    LocationInBuilding = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Freezers", x => x.FreezerID);
                });

            migrationBuilder.CreateTable(
                name: "tbl_SampleTypes",
                columns: table => new
                {
                    SampleTypeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeName = table.Column<string>(type: "text", nullable: false),
                    IsConsumable = table.Column<bool>(type: "boolean", nullable: false),
                    MaxShippableUnits = table.Column<int>(type: "integer", nullable: true),
                    LocalReserveUnits = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_SampleTypes", x => x.SampleTypeID);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Studies",
                columns: table => new
                {
                    StudyID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyCode = table.Column<string>(type: "text", nullable: false),
                    StudyName = table.Column<string>(type: "text", nullable: true),
                    PrincipalInvestigator = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Studies", x => x.StudyID);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Approvals",
                columns: table => new
                {
                    ApprovalID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalType = table.Column<string>(type: "text", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EDApproverUserId = table.Column<string>(type: "text", nullable: true),
                    EDApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EDApprovalStatus = table.Column<string>(type: "text", nullable: true),
                    EDComments = table.Column<string>(type: "text", nullable: true),
                    RegulatoryApproverUserId = table.Column<string>(type: "text", nullable: true),
                    RegulatoryApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegulatoryApprovalStatus = table.Column<string>(type: "text", nullable: true),
                    RegulatoryComments = table.Column<string>(type: "text", nullable: true),
                    PIApproverUserId = table.Column<string>(type: "text", nullable: true),
                    PIApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PIApprovalStatus = table.Column<string>(type: "text", nullable: true),
                    PIComments = table.Column<string>(type: "text", nullable: true),
                    OverallStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Approvals", x => x.ApprovalID);
                    table.CheckConstraint("CK_Approvals_ApprovalType", "\"ApprovalType\" IN ('Shipment', 'Discard', 'SingleAliquotException')");
                    table.CheckConstraint("CK_Approvals_EDApprovalStatus", "\"EDApprovalStatus\" IS NULL OR \"EDApprovalStatus\" IN ('Pending', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_Approvals_OverallStatus", "\"OverallStatus\" IN ('Pending', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_Approvals_PIApprovalStatus", "\"PIApprovalStatus\" IS NULL OR \"PIApprovalStatus\" IN ('Pending', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_Approvals_RegulatoryApprovalStatus", "\"RegulatoryApprovalStatus\" IS NULL OR \"RegulatoryApprovalStatus\" IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_tbl_Approvals_AspNetUsers_EDApproverUserId",
                        column: x => x.EDApproverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Approvals_AspNetUsers_PIApproverUserId",
                        column: x => x.PIApproverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Approvals_AspNetUsers_RegulatoryApproverUserId",
                        column: x => x.RegulatoryApproverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Approvals_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_AuditLog",
                columns: table => new
                {
                    AuditLogID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableName = table.Column<string>(type: "text", nullable: false),
                    RecordID = table.Column<int>(type: "integer", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangedByUserId = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AuditLog", x => x.AuditLogID);
                    table.ForeignKey(
                        name: "FK_tbl_AuditLog_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_UserProfiles",
                columns: table => new
                {
                    UserProfileID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AspNetUserId = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: true),
                    CanApproveShipments = table.Column<bool>(type: "boolean", nullable: false),
                    CanApproveDiscards = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_UserProfiles", x => x.UserProfileID);
                    table.ForeignKey(
                        name: "FK_tbl_UserProfiles_AspNetUsers_AspNetUserId",
                        column: x => x.AspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "tbl_ShipmentBatches",
                columns: table => new
                {
                    BatchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ImportedByUserId = table.Column<string>(type: "text", nullable: true),
                    RequestorName = table.Column<string>(type: "text", nullable: true),
                    RequestorEmail = table.Column<string>(type: "text", nullable: true),
                    TotalRequested = table.Column<int>(type: "integer", nullable: false),
                    TotalAvailable = table.Column<int>(type: "integer", nullable: false),
                    TotalNotFound = table.Column<int>(type: "integer", nullable: false),
                    TotalPreviouslyShipped = table.Column<int>(type: "integer", nullable: false),
                    TotalDiscarded = table.Column<int>(type: "integer", nullable: false),
                    TotalNotYetReceived = table.Column<int>(type: "integer", nullable: false),
                    ApprovalID = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending Approval")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ShipmentBatches", x => x.BatchID);
                    table.CheckConstraint("CK_ShipmentBatches_Status", "\"Status\" IN ('Pending Approval', 'Approved', 'Rejected', 'Processing', 'Shipped')");
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentBatches_AspNetUsers_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentBatches_tbl_Approvals_ApprovalID",
                        column: x => x.ApprovalID,
                        principalTable: "tbl_Approvals",
                        principalColumn: "ApprovalID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Racks",
                columns: table => new
                {
                    RackID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RackName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CompartmentID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Racks", x => x.RackID);
                    table.ForeignKey(
                        name: "FK_tbl_Racks_tbl_Compartments_CompartmentID",
                        column: x => x.CompartmentID,
                        principalTable: "tbl_Compartments",
                        principalColumn: "CompartmentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Boxes",
                columns: table => new
                {
                    BoxID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoxLabel = table.Column<string>(type: "text", nullable: false),
                    BoxType = table.Column<string>(type: "text", nullable: false),
                    BoxCategory = table.Column<string>(type: "text", nullable: false, defaultValue: "Standard"),
                    RackID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Boxes", x => x.BoxID);
                    table.CheckConstraint("CK_Boxes_BoxCategory", "\"BoxCategory\" IN ('Standard', 'Temp', 'Trash', 'Shipping')");
                    table.CheckConstraint("CK_Boxes_BoxType", "\"BoxType\" IN ('81-slot', '100-slot', 'Filter Paper Binder')");
                    table.ForeignKey(
                        name: "FK_tbl_Boxes_tbl_Racks_RackID",
                        column: x => x.RackID,
                        principalTable: "tbl_Racks",
                        principalColumn: "RackID");
                });

            migrationBuilder.CreateTable(
                name: "tbl_Shipments",
                columns: table => new
                {
                    ShipmentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchID = table.Column<int>(type: "integer", nullable: true),
                    ShipmentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Courier = table.Column<string>(type: "text", nullable: true),
                    TrackingNumber = table.Column<string>(type: "text", nullable: true),
                    DestinationAddress = table.Column<string>(type: "text", nullable: true),
                    ShippedByUserId = table.Column<string>(type: "text", nullable: true),
                    IsEntireBox = table.Column<bool>(type: "boolean", nullable: false),
                    ShippedBoxID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Shipments", x => x.ShipmentID);
                    table.ForeignKey(
                        name: "FK_tbl_Shipments_AspNetUsers_ShippedByUserId",
                        column: x => x.ShippedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Shipments_tbl_Boxes_ShippedBoxID",
                        column: x => x.ShippedBoxID,
                        principalTable: "tbl_Boxes",
                        principalColumn: "BoxID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Shipments_tbl_ShipmentBatches_BatchID",
                        column: x => x.BatchID,
                        principalTable: "tbl_ShipmentBatches",
                        principalColumn: "BatchID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Specimens",
                columns: table => new
                {
                    SpecimenID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BarcodeID = table.Column<string>(type: "text", nullable: false),
                    LegacyID = table.Column<string>(type: "text", nullable: true),
                    ParticipantID = table.Column<string>(type: "text", nullable: true),
                    StudyID = table.Column<int>(type: "integer", nullable: true),
                    SampleTypeID = table.Column<int>(type: "integer", nullable: true),
                    CollectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BoxID = table.Column<int>(type: "integer", nullable: true),
                    PositionRow = table.Column<int>(type: "integer", nullable: true),
                    PositionCol = table.Column<int>(type: "integer", nullable: true),
                    RemainingSpots = table.Column<int>(type: "integer", nullable: true),
                    SpotsShippedInternational = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SpotsReservedLocal = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AliquotNumber = table.Column<int>(type: "integer", nullable: true),
                    DiscardApprovalID = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "In-Stock")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Specimens", x => x.SpecimenID);
                    table.CheckConstraint("CK_Specimens_AliquotNumber", "\"AliquotNumber\" IS NULL OR \"AliquotNumber\" IN (1, 2)");
                    table.CheckConstraint("CK_Specimens_Status", "\"Status\" IN ('In-Stock', 'Staged', 'Shipped', 'Missing', 'Depleted', 'Discarded', 'Temp', 'Not Yet Received')");
                    table.ForeignKey(
                        name: "FK_tbl_Specimens_tbl_Approvals_DiscardApprovalID",
                        column: x => x.DiscardApprovalID,
                        principalTable: "tbl_Approvals",
                        principalColumn: "ApprovalID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Specimens_tbl_Boxes_BoxID",
                        column: x => x.BoxID,
                        principalTable: "tbl_Boxes",
                        principalColumn: "BoxID");
                    table.ForeignKey(
                        name: "FK_tbl_Specimens_tbl_SampleTypes_SampleTypeID",
                        column: x => x.SampleTypeID,
                        principalTable: "tbl_SampleTypes",
                        principalColumn: "SampleTypeID");
                    table.ForeignKey(
                        name: "FK_tbl_Specimens_tbl_Studies_StudyID",
                        column: x => x.StudyID,
                        principalTable: "tbl_Studies",
                        principalColumn: "StudyID");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ShipmentContents",
                columns: table => new
                {
                    ShipmentContentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipmentID = table.Column<int>(type: "integer", nullable: false),
                    SpecimenID = table.Column<int>(type: "integer", nullable: false),
                    ConditionAtShipment = table.Column<string>(type: "text", nullable: true),
                    ShippingBoxPosition = table.Column<string>(type: "text", nullable: true),
                    SpotsUsed = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ShipmentContents", x => x.ShipmentContentID);
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentContents_tbl_Shipments_ShipmentID",
                        column: x => x.ShipmentID,
                        principalTable: "tbl_Shipments",
                        principalColumn: "ShipmentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentContents_tbl_Specimens_SpecimenID",
                        column: x => x.SpecimenID,
                        principalTable: "tbl_Specimens",
                        principalColumn: "SpecimenID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ShipmentRequests",
                columns: table => new
                {
                    RequestID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchID = table.Column<int>(type: "integer", nullable: true),
                    RequestedBarcode = table.Column<string>(type: "text", nullable: false),
                    RequestedSampleTypeID = table.Column<int>(type: "integer", nullable: true),
                    RequestorName = table.Column<string>(type: "text", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MatchedSpecimenID = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ShipmentRequests", x => x.RequestID);
                    table.CheckConstraint("CK_ShipmentRequests_Status", "\"Status\" IN ('Pending', 'Staged', 'Shipped', 'Not Found', 'Previously Shipped', 'Discarded', 'Not Yet Received')");
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentRequests_tbl_SampleTypes_RequestedSampleTypeID",
                        column: x => x.RequestedSampleTypeID,
                        principalTable: "tbl_SampleTypes",
                        principalColumn: "SampleTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentRequests_tbl_ShipmentBatches_BatchID",
                        column: x => x.BatchID,
                        principalTable: "tbl_ShipmentBatches",
                        principalColumn: "BatchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_ShipmentRequests_tbl_Specimens_MatchedSpecimenID",
                        column: x => x.MatchedSpecimenID,
                        principalTable: "tbl_Specimens",
                        principalColumn: "SpecimenID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_FilterPaperUsages",
                columns: table => new
                {
                    FilterPaperUsageID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpecimenID = table.Column<int>(type: "integer", nullable: false),
                    UsageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    SpotsUsed = table.Column<int>(type: "integer", nullable: false),
                    IsInternationalShipment = table.Column<bool>(type: "boolean", nullable: false),
                    UsedByUserId = table.Column<string>(type: "text", nullable: true),
                    ShipmentContentID = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_FilterPaperUsages", x => x.FilterPaperUsageID);
                    table.CheckConstraint("CK_FilterPaperUsages_SpotsUsed", "\"SpotsUsed\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_tbl_FilterPaperUsages_AspNetUsers_UsedByUserId",
                        column: x => x.UsedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_FilterPaperUsages_tbl_ShipmentContents_ShipmentContentID",
                        column: x => x.ShipmentContentID,
                        principalTable: "tbl_ShipmentContents",
                        principalColumn: "ShipmentContentID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tbl_FilterPaperUsages_tbl_Specimens_SpecimenID",
                        column: x => x.SpecimenID,
                        principalTable: "tbl_Specimens",
                        principalColumn: "SpecimenID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Approvals_EDApproverUserId",
                table: "tbl_Approvals",
                column: "EDApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Approvals_PIApproverUserId",
                table: "tbl_Approvals",
                column: "PIApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Approvals_RegulatoryApproverUserId",
                table: "tbl_Approvals",
                column: "RegulatoryApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Approvals_RequestedByUserId",
                table: "tbl_Approvals",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TableRecord",
                table: "tbl_AuditLog",
                columns: new[] { "TableName", "RecordID" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "tbl_AuditLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_AuditLog_ChangedByUserId",
                table: "tbl_AuditLog",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Boxes_BoxLabel",
                table: "tbl_Boxes",
                column: "BoxLabel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Boxes_RackID",
                table: "tbl_Boxes",
                column: "RackID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Compartments_FreezerID_CompartmentName",
                table: "tbl_Compartments",
                columns: new[] { "FreezerID", "CompartmentName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_FilterPaperUsages_ShipmentContentID",
                table: "tbl_FilterPaperUsages",
                column: "ShipmentContentID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_FilterPaperUsages_SpecimenID",
                table: "tbl_FilterPaperUsages",
                column: "SpecimenID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_FilterPaperUsages_UsedByUserId",
                table: "tbl_FilterPaperUsages",
                column: "UsedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Freezers_FreezerName",
                table: "tbl_Freezers",
                column: "FreezerName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Racks_CompartmentID",
                table: "tbl_Racks",
                column: "CompartmentID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_SampleTypes_TypeName",
                table: "tbl_SampleTypes",
                column: "TypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentBatches_ApprovalID",
                table: "tbl_ShipmentBatches",
                column: "ApprovalID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentBatches_ImportedByUserId",
                table: "tbl_ShipmentBatches",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentContents_ShipmentID",
                table: "tbl_ShipmentContents",
                column: "ShipmentID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentContents_SpecimenID",
                table: "tbl_ShipmentContents",
                column: "SpecimenID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentRequests_BatchID",
                table: "tbl_ShipmentRequests",
                column: "BatchID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentRequests_MatchedSpecimenID",
                table: "tbl_ShipmentRequests",
                column: "MatchedSpecimenID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ShipmentRequests_RequestedSampleTypeID",
                table: "tbl_ShipmentRequests",
                column: "RequestedSampleTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Shipments_BatchID",
                table: "tbl_Shipments",
                column: "BatchID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Shipments_ShippedBoxID",
                table: "tbl_Shipments",
                column: "ShippedBoxID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Shipments_ShippedByUserId",
                table: "tbl_Shipments",
                column: "ShippedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_ParticipantID",
                table: "tbl_Specimens",
                column: "ParticipantID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_BarcodeID",
                table: "tbl_Specimens",
                column: "BarcodeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_BoxID_PositionRow_PositionCol",
                table: "tbl_Specimens",
                columns: new[] { "BoxID", "PositionRow", "PositionCol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_DiscardApprovalID",
                table: "tbl_Specimens",
                column: "DiscardApprovalID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_SampleTypeID",
                table: "tbl_Specimens",
                column: "SampleTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_Status",
                table: "tbl_Specimens",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Specimens_StudyID",
                table: "tbl_Specimens",
                column: "StudyID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Studies_StudyCode",
                table: "tbl_Studies",
                column: "StudyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserProfiles_AspNetUserId",
                table: "tbl_UserProfiles",
                column: "AspNetUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "tbl_AuditLog");

            migrationBuilder.DropTable(
                name: "tbl_FilterPaperUsages");

            migrationBuilder.DropTable(
                name: "tbl_ShipmentRequests");

            migrationBuilder.DropTable(
                name: "tbl_UserProfiles");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "tbl_ShipmentContents");

            migrationBuilder.DropTable(
                name: "tbl_Shipments");

            migrationBuilder.DropTable(
                name: "tbl_Specimens");

            migrationBuilder.DropTable(
                name: "tbl_ShipmentBatches");

            migrationBuilder.DropTable(
                name: "tbl_Boxes");

            migrationBuilder.DropTable(
                name: "tbl_SampleTypes");

            migrationBuilder.DropTable(
                name: "tbl_Studies");

            migrationBuilder.DropTable(
                name: "tbl_Approvals");

            migrationBuilder.DropTable(
                name: "tbl_Racks");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "tbl_Compartments");

            migrationBuilder.DropTable(
                name: "tbl_Freezers");
        }
    }
}

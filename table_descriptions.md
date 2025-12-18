# Database Schema: Specimen Tracking And Storage Information System (STASIS)

## Document Information

| Field | Value |
|-------|-------|
| Version | 2.1 |
| Last Updated | December 2024 |
| Related Documents | system_requirements_updated.md, STASIS_create_tables.sql |

---

## Authentication & User Management

This system uses **ASP.NET Identity** for user authentication and role management.

### ASP.NET Identity Tables (Created by Application)

| Table | Purpose |
|-------|---------|
| `AspNetUsers` | Core user records (Id, UserName, Email, PasswordHash, etc.) |
| `AspNetRoles` | Role definitions: **Admin**, **Write**, **Read** |
| `AspNetUserRoles` | Junction table linking users to roles |
| `AspNetUserClaims` | User-specific claims |
| `AspNetRoleClaims` | Role-specific claims |
| `AspNetUserLogins` | External login providers |
| `AspNetUserTokens` | Authentication tokens |

### User Roles

| Role | Description |
|------|-------------|
| Admin | Full system access including user management and approvals |
| Write | Can add, move, and update sample records |
| Read | View-only access to sample records and reports |

### `tbl_UserProfiles`

Extends ASP.NET Identity with STASIS-specific user attributes.

| Field | Type | Description |
|-------|------|-------------|
| UserProfileID | int (PK) | Auto-increment primary key |
| AspNetUserId | nvarchar(450) | FK to AspNetUsers.Id (unique) |
| Department | nvarchar(100) | User's department |
| CanApproveShipments | bit | Permission to approve shipment requests |
| CanApproveDiscards | bit | Permission to approve sample disposal |

---

## 1. Core Inventory Tables

### `tbl_Freezers`

Top-level storage units (freezers, cold rooms).

| Field | Type | Description |
|-------|------|-------------|
| FreezerID | int (PK) | Auto-increment primary key |
| FreezerName | nvarchar(50) | Unique freezer name (e.g., "-80 Freezer A") |
| Temperature | int | Storage temperature in Celsius |
| LocationInBuilding | nvarchar(100) | Physical location description |

### `tbl_Racks`

Subdivisions within a freezer (shelves, racks, compartments).

| Field | Type | Description |
|-------|------|-------------|
| RackID | int (PK) | Auto-increment primary key |
| RackName | nvarchar(50) | Rack/shelf identifier |
| FreezerID | int (FK) | Parent freezer |

### `tbl_Boxes`

Physical containers holding samples.

| Field | Type | Description |
|-------|------|-------------|
| BoxID | int (PK) | Auto-increment primary key |
| BoxLabel | nvarchar(50) | Unique box identifier |
| BoxType | nvarchar(50) | "81-slot", "100-slot", or "Filter Paper Binder" |
| BoxCategory | nvarchar(50) | "Standard", "Temp", "Trash", or "Shipping" |
| RackID | int (FK) | Parent rack (nullable for Temp/Trash/Shipping) |

**Box Categories:**

| Category | Description |
|----------|-------------|
| Standard | Normal storage box in freezer |
| Temp | Temporary holding for unassigned samples |
| Trash | Samples awaiting/approved for disposal |
| Shipping | Container for outgoing shipment |

### `tbl_Specimens`

Core table - each row represents one physical sample.

| Field | Type | Description |
|-------|------|-------------|
| SpecimenID | int (PK) | Auto-increment primary key |
| BarcodeID | nvarchar(100) | Unique barcode identifier |
| LegacyID | nvarchar(100) | Previous system identifier (if any) |
| StudyID | int (FK) | Source study/project |
| SampleTypeID | int (FK) | Sample type |
| CollectionDate | date | When sample was collected |
| BoxID | int (FK) | Current box location (nullable for Temp) |
| PositionRow | int | Row position in box |
| PositionCol | int | Column position in box |
| Status | nvarchar(50) | Current status (see below) |
| RemainingSpots | int | Filter Paper: spots remaining (0-4) |
| SpotsShippedInternational | int | Filter Paper: spots sent internationally |
| SpotsReservedLocal | int | Filter Paper: spots reserved for local use |
| AliquotNumber | int | Plasma: aliquot number (1 or 2) |
| DiscardApprovalID | int (FK) | Link to approval record if being discarded |

**Specimen Status Values:**

| Status | Description |
|--------|-------------|
| In-Stock | Available in inventory |
| Staged | Pulled from freezer, waiting to ship |
| Shipped | Sent to destination |
| Missing | Requested but physically not found |
| Depleted | All spots/aliquots used (Filter Paper) |
| Discarded | Sample was disposed of |
| Temp | In temporary holding, unassigned |

---

## 2. Reference Tables

### `tbl_Studies`

Registry of research projects submitting samples.

| Field | Type | Description |
|-------|------|-------------|
| StudyID | int (PK) | Auto-increment primary key |
| StudyCode | nvarchar(50) | Unique study code (e.g., "MAL-001") |
| StudyName | nvarchar(200) | Full study name |
| PrincipalInvestigator | nvarchar(100) | PI name |

### `tbl_SampleTypes`

Defines valid sample types with shipping restrictions.

| Field | Type | Description |
|-------|------|-------------|
| SampleTypeID | int (PK) | Auto-increment primary key |
| TypeName | nvarchar(50) | Unique type name (e.g., "Plasma") |
| IsConsumable | bit | True if sample can be partially used (e.g., Filter Paper) |
| MaxShippableUnits | int | Max units that can be shipped internationally |
| LocalReserveUnits | int | Units that must be reserved for local use |

**Default Sample Types:**

| Type | IsConsumable | MaxShippable | LocalReserve |
|------|--------------|--------------|--------------|
| Plasma | No | 1 | 1 |
| Buffy Coat | No | NULL | NULL |
| Filter Paper | Yes | 2 | 2 |
| Whole Blood | No | NULL | NULL |

---

## 3. Shipment Tables

### `tbl_ShipmentBatches`

Groups shipment requests imported together for approval workflow.

| Field | Type | Description |
|-------|------|-------------|
| BatchID | int (PK) | Auto-increment primary key |
| ImportDate | datetime | When request file was imported |
| ImportedByUserId | nvarchar(450) | User who imported the request |
| RequestorName | nvarchar(100) | Name of person/institution requesting samples |
| RequestorEmail | nvarchar(100) | Requestor contact email |
| TotalRequested | int | Count of items in request |
| TotalAvailable | int | Count found in inventory |
| TotalNotFound | int | Count not in inventory |
| TotalPreviouslyShipped | int | Count already shipped |
| TotalDiscarded | int | Count that were discarded |
| TotalNotYetReceived | int | Count not yet in Kampala |
| ApprovalID | int (FK) | Link to approval record |
| Status | nvarchar(50) | Batch status (see below) |

**Batch Status Values:**

| Status | Description |
|--------|-------------|
| Pending Approval | Awaiting ED/Regulatory sign-off |
| Approved | Approved, ready for processing |
| Rejected | Request was denied |
| Processing | Samples being pulled/staged |
| Shipped | All items shipped |

### `tbl_ShipmentRequests`

Individual line items from imported shipment request files.

| Field | Type | Description |
|-------|------|-------------|
| RequestID | int (PK) | Auto-increment primary key |
| BatchID | int (FK) | Parent batch |
| RequestedBarcode | nvarchar(100) | Barcode from request file |
| RequestedSampleTypeID | int (FK) | Requested sample type |
| RequestorName | nvarchar(100) | Requestor name |
| RequestDate | date | Date of request |
| MatchedSpecimenID | int (FK) | Matched specimen (if found) |
| Status | nvarchar(50) | Request line status (see below) |

**Request Status Values:**

| Status | Description |
|--------|-------------|
| Pending | Request received, not yet processed |
| Staged | Sample pulled, waiting for shipment |
| Shipped | Sample has been sent |
| Not Found | Sample not in inventory |
| Previously Shipped | Sample was already shipped |
| Discarded | Sample was disposed of |
| Not Yet Received | Sample not yet in Kampala |

### `tbl_Shipments`

Represents physical outgoing packages (manifests).

| Field | Type | Description |
|-------|------|-------------|
| ShipmentID | int (PK) | Auto-increment primary key |
| BatchID | int (FK) | Parent batch |
| ShipmentDate | date | Date of shipment |
| Courier | nvarchar(100) | Shipping carrier |
| TrackingNumber | nvarchar(100) | Carrier tracking number |
| DestinationAddress | nvarchar(255) | Shipping destination |
| ShippedByUserId | nvarchar(450) | User who processed shipment |
| IsEntireBox | bit | True if shipping entire box |
| ShippedBoxID | int (FK) | Box being shipped (if IsEntireBox) |

### `tbl_ShipmentContents`

Junction table linking specimens to shipments.

| Field | Type | Description |
|-------|------|-------------|
| ShipmentContentID | int (PK) | Auto-increment primary key |
| ShipmentID | int (FK) | Parent shipment |
| SpecimenID | int (FK) | Specimen being shipped |
| ConditionAtShipment | nvarchar(100) | Condition notes (e.g., "On Dry Ice") |
| ShippingBoxPosition | nvarchar(20) | Position in shipping container (e.g., "A1") |
| SpotsUsed | int | Filter Paper: spots used in this shipment |

---

## 4. Approval Workflow

### `tbl_Approvals`

Records approval decisions for shipments and sample disposal.

| Field | Type | Description |
|-------|------|-------------|
| ApprovalID | int (PK) | Auto-increment primary key |
| ApprovalType | nvarchar(50) | "Shipment", "Discard", or "SingleAliquotException" |
| RequestedByUserId | nvarchar(450) | User who initiated request |
| RequestedDate | datetime | When approval was requested |
| EDApproverUserId | nvarchar(450) | ED who reviewed |
| EDApprovalDate | datetime | When ED reviewed |
| EDApprovalStatus | nvarchar(20) | "Pending", "Approved", or "Rejected" |
| EDComments | nvarchar(500) | ED comments |
| RegulatoryApproverUserId | nvarchar(450) | Regulatory staff who reviewed |
| RegulatoryApprovalDate | datetime | When Regulatory reviewed |
| RegulatoryApprovalStatus | nvarchar(20) | "Pending", "Approved", or "Rejected" |
| RegulatoryComments | nvarchar(500) | Regulatory comments |
| PIApproverUserId | nvarchar(450) | PI who reviewed (for discards) |
| PIApprovalDate | datetime | When PI reviewed |
| PIApprovalStatus | nvarchar(20) | "Pending", "Approved", or "Rejected" |
| PIComments | nvarchar(500) | PI comments |
| OverallStatus | nvarchar(20) | "Pending", "Approved", or "Rejected" |

---

## 5. Audit Trail

### `tbl_AuditLog`

Tracks all changes to specimen and box records.

| Field | Type | Description |
|-------|------|-------------|
| AuditLogID | int (PK) | Auto-increment primary key |
| TableName | nvarchar(100) | Table that was modified |
| RecordID | int | Primary key of modified record |
| FieldName | nvarchar(100) | Field that changed |
| OldValue | nvarchar(max) | Previous value |
| NewValue | nvarchar(max) | New value |
| ChangedByUserId | nvarchar(450) | User who made change |
| Timestamp | datetime | When change occurred |

**Indexes:**
- `IX_AuditLog_TableRecord` on (TableName, RecordID)
- `IX_AuditLog_Timestamp` on (Timestamp DESC)

---

## 6. Entity Relationship Diagram

```
┌─────────────────┐
│  AspNetUsers    │
└────────┬────────┘
         │
         ├──────────────────────────────────────────────┐
         │                                              │
         ▼                                              ▼
┌─────────────────┐                          ┌─────────────────┐
│ tbl_UserProfiles│                          │  tbl_AuditLog   │
└─────────────────┘                          └─────────────────┘
         
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│  tbl_Freezers   │──────▶│   tbl_Racks     │──────▶│   tbl_Boxes     │
└─────────────────┘   1:M └─────────────────┘   1:M └────────┬────────┘
                                                             │
                                                             │ 1:M
                                                             ▼
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│  tbl_Studies    │──────▶│  tbl_Specimens  │◀──────│ tbl_SampleTypes │
└─────────────────┘   1:M └────────┬────────┘   M:1 └─────────────────┘
                                   │
                                   │ 1:M
                                   ▼
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│ tbl_Approvals   │◀──────│tbl_ShipmentBatch│──────▶│tbl_ShipmentReqs │
└─────────────────┘   1:M └────────┬────────┘   1:M └─────────────────┘
         │                         │
         │                         │ 1:M
         ▼                         ▼
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│  tbl_Specimens  │       │  tbl_Shipments  │──────▶│tbl_ShipContents │
│(DiscardApproval)│       └─────────────────┘   1:M └─────────────────┘
└─────────────────┘
```

---

## 7. System & Seed Data

The following data is created automatically by the database creation script to populate the initial environment.

### User Roles (ASP.NET Identity)

| Role | NormalizedName | Description |
|------|----------------|-------------|
| Admin | ADMIN | Full system access |
| Write | WRITE | Can add/move samples |
| Read | READ | View-only access |

### Studies

| StudyCode | StudyName | Principal Investigator |
|-----------|-----------|------------------------|
| PRISM | PRISM Study | Dr. Dorsey |
| SAPPHIRE | SAPPHIRE Study | Dr. Jane |
| PBO | PBO Study | Dr. Sam |

### Default Sample Types

| TypeName | IsConsumable | MaxShippable | LocalReserve |
|----------|--------------|--------------|--------------|
| Plasma | No | 1 | 1 |
| Buffy Coat | No | NULL | NULL |
| Whole Blood | No | NULL | NULL |
| Filter Paper | Yes | 2 | 2 |

### Storage Infrastructure

**Freezers:**

| FreezerName | Temperature | Location |
|-------------|-------------|----------|
| -80 Freezer A | -80°C | Room 101 |
| -20 Freezer B | -20°C | Room 102 |

**Racks:**

| RackName | Parent Freezer |
|----------|----------------|
| Rack 1 | -80 Freezer A |
| Rack 2 | -80 Freezer A |
| Rack 3 | -80 Freezer A |
| Rack 4 | -80 Freezer A |
| Shelf 1 | -20 Freezer B |

### Boxes

| BoxLabel | BoxType | Category | Rack Location | Purpose |
|----------|---------|----------|---------------|---------|
| BOX-001 | 81-slot | Standard | Rack 1 | Initial Plasma storage |
| BOX-002 | 81-slot | Standard | Rack 1 | Initial Buffy Coat storage |
| BOX-003 | 100-slot | Standard | Rack 2 | Initial Whole Blood storage |
| FP-BINDER-01 | Filter Paper Binder | Standard | Shelf 1 | Initial Filter Paper storage |
| SYSTEM-TEMP | 100-slot | Temp | *None* | Temporary holding for unassigned samples |
| SYSTEM-TRASH | 100-slot | Trash | *None* | Samples pending disposal approval |

### Specimen Data Summary
*The script also generates 140 initial test specimens distributed across the boxes listed above.*
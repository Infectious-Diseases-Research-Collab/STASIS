# Conceptual Database Schema: Specimen Tracking System

## 1. Core Inventory Tables
These tables manage the physical existence and location of samples.

### `tbl_Specimens`
* **Functionality:** The heart of the system. Each row represents one unique physical tube, card, or vial.
* **Key Logic:**
    * Stores the unique `BarcodeID`.
    * Links to `tbl_Studies` (Source Project).
    * Links to `tbl_Boxes` (Where is it currently?).
    * Stores `PositionRow` and `PositionCol` (Coordinates inside the box).
    * **Filter Paper Logic:** Includes a field for `RemainingSpots` (Integer) to handle the multi-use requirement.

### `tbl_Boxes`
* **Functionality:** Represents the physical container holding samples (e.g., a 9x9 cryo box).
* **Key Logic:**
    * Has a unique `BoxLabel` or `BoxID`.
    * Includes `BoxType` (e.g., "81-slot", "100-slot", "Filter Paper Binder") to define valid row/column ranges.
    * Links to `tbl_Racks` or `tbl_Freezers` to establish location.

### `tbl_Freezers` (Storage Units)
* **Functionality:** Defines the top-level storage units (e.g., "-80 Freezer A", "Walk-in Cold Room").
* **Key Logic:**
    * Stores metadata like `Temperature`, `LocationInBuilding`.
    * Acts as the parent for Racks/Shelves.

### `tbl_Racks` (Shelves/Compartments)
* **Functionality:** Represents the subdivision within a freezer (e.g., "Shelf 1", "Rack B").
* **Key Logic:**
    * Links to `tbl_Freezers`.
    * Allows you to move an entire Rack of boxes if necessary.

---

## 2. Reference & Metadata Tables
These tables ensure data consistency (preventing typos in study names or sample types).

### `tbl_Studies` (Projects)
* **Functionality:** A registry of all projects submitting samples.
* **Key Logic:**
    * `StudyCode` (e.g., "MAL-001").
    * `PrincipalInvestigator`.

### `tbl_SampleTypes`
* **Functionality:** Defines valid sample types to prevent data entry errors (e.g., enforcing "Plasma" vs. "PL" vs. "P").
* **Key Logic:**
    * `TypeName` (e.g., "Plasma", "Buffy Coat").
    * `IsConsumable` (Boolean: e.g., Filter paper is consumable over time, Plasma is usually single-use).

---

## 3. Shipment & Request Tables
These tables handle the "Outbound" workflow, separating *what was asked for* from *what was shipped*.

### `tbl_ShipmentRequests` (The "Wish List")
* **Functionality:** Stores the imported rows from the Excel spreadsheet requests.
* **Key Logic:**
    * Stores the `RequestedBarcode`, `RequestorName`, `RequestDate`.
    * **Status Field:** Tracks the lifecycle of this specific request line item (`Pending`, `Staged`, `Shipped`, `Not Found`, `Previously Shipped`).
    * This table persists even if the sample is never found, preserving the history of the request.

### `tbl_Shipments` (The "Manifest")
* **Functionality:** Represents a physical outgoing package.
* **Key Logic:**
    * `ShipmentDate`, `Courier`, `TrackingNumber`, `DestinationAddress`.
    * Generated only when items are actually boxed up and leaving.

### `tbl_ShipmentContents`
* **Functionality:** The junction table linking a real `Specimen` to a `Shipment`.
* **Key Logic:**
    * Links `tbl_Specimens` to `tbl_Shipments`.
    * Records the condition at shipment (e.g., "Shipped on Dry Ice").
    * For Filter Paper: Records how many spots were used/subtracted during this specific shipment.

---

## 4. System & Audit Tables

### `tbl_AuditLog`
* **Functionality:** The "Black Box" recorder.
* **Key Logic:**
    * **Trigger Based:** Every time a record in `tbl_Specimens` or `tbl_Boxes` is updated, a row is added here.
    * Fields: `TableName`, `RecordID`, `FieldName`, `OldValue`, `NewValue`, `ChangedBy`, `Timestamp`.
    * *Crucial for tracing if a sample was moved and by whom.*

### `tbl_Users`
* **Functionality:** Authentication and permission management.
* **Key Logic:**
    * `Username`, `Role` (Admin, Technician, Read-Only).

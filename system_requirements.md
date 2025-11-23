# Requirements Specification: Specimen Tracking And Storage Information System (STASIS)

## 1. Introduction
### 1.1 Purpose
The purpose of the pecimen Tracking And Storage Information System (STASIS) is to manage the lifecycle of biological samples (specimens) from receipt through storage and final shipment. The system will act as the "Source of Truth" for the physical location of every sample in the facility.

### 1.2 Scope
* **Inbound:** Receiving samples from various study projects (e.g., Plasma, Buffy Coat, Filter Paper).
* **Storage:** Managing a hierarchical storage structure (Freezer $\\to$ Rack $\\to$ Box $\\to$ Position).
* **Internal Movement:** Tracking the relocation of individual samples or entire boxes.
* **Outbound:** Processing shipment requests, retrieving samples, and logging shipment details.

---

## 2. Functional Requirements

### 2.1 Sample Accessioning (Intake)
The system must allow for the entry of new samples into the inventory.

* **REQ-ACC-01:** The system shall support the registration of individual samples via manual entry or barcode scanning.
* **REQ-ACC-02:** The system shall support **Bulk Import** (CSV/Excel/SQL Interface) to register large batches of samples arriving from different projects.
* **REQ-ACC-03:** Each specimen record must capture, at minimum:
    * Unique Barcode ID
    * Study/Project Name
    * Sample Type (e.g., Plasma, Filter Paper, Whole Blood)
    * Collection Date
* **REQ-ACC-04:** The system must validate that a Barcode ID is unique within the system to prevent duplicate records.

### 2.2 Storage & Inventory Management
The system must map the physical location of every sample.

* **REQ-INV-01 (Location Hierarchy):** The system must define storage locations using the following hierarchy:
    * `Freezer` (Name/ID)
    * `Shelf/Rack` (Location within Freezer)
    * `Box` (Container ID/Number)
    * `Position` (Row/Column or Numeric Slot 1-81/1-100)
* **REQ-INV-02 (Box Management):** Users must be able to assign a specific location (Freezer/Shelf) to a Box.
* **REQ-INV-03 (Sample Placement):** Users must be able to assign a specific coordinate (Row/Column) to a sample within a Box.
* **REQ-INV-04 (Conflict Detection):** The system must prevent placing a sample in a box position that is already occupied.

### 2.3 Specimen Movement
The system must track movement to maintain chain of custody.

* **REQ-MOV-01 (Individual Move):** The system shall allow users to move a single sample from "Box A" to "Box B".
* **REQ-MOV-02 (Bulk Move/Re-boxing):** The system shall allow users to scan all samples in a physical box to update their digital location to match the physical reality.
* **REQ-MOV-03 (Box Relocation):** The system shall allow users to move an entire Box from one Freezer/Rack to another without having to scan individual samples inside.
* **REQ-MOV-04 (Empty Box Logic):** If all samples are removed from a Box, or if a Box is physically removed from a Freezer, the system must automatically update the Freezer/Rack location of that Box to "None/Unassigned" to free up capacity.

### 2.4 Shipment Management
The system must manage the retrieval and dispatch of samples based on external requests.

* **REQ-SHP-01 (Request Import):** The system shall allow users to import a "Shipment Request" (via Excel/CSV) containing:
    * External ID/Barcode
    * Requested Sample Type
    * Requestor Name
    * Request Date
* **REQ-SHP-02 (Availability Check):** Upon importing a request, the system must run a query to match requested items against current inventory.
* **REQ-SHP-03 (Status Workflow):** The system shall track the status of a requested sample. Valid statuses include:
    * `In Inventory` (Available)
    * `Staged` (Pulled from freezer, waiting to ship)
    * `Shipped` (Sent to destination)
    * `Not Found` (Requested but physically missing)
    * `Previously Shipped` (Requested but record shows it left already)
* **REQ-SHP-04 (Shipping Container):** The system must record the *new* storage configuration for shipment (e.g., putting samples into a Shipping Box, specifying Row/Col within the shipping container).

### 2.5 Special Handling: Filter Papers
Specific logic is required for Filter Paper samples which may be used multiple times (punched) before being depleted.

* **REQ-SPL-01:** For sample type "Filter Paper," the system must track usage/remaining quantity.
* **REQ-SPL-02:** The system shall allow up to 4 distinct usages (cohort/request dates) per Filter Paper sample to track remaining "spots."
* **REQ-SPL-03:** A Filter Paper sample should only be marked as fully "Shipped/Depleted" when all spots are used or the entire card is sent.

---

## 3. Reporting & Auditing Requirements

* **REQ-RPT-01 (Search):** Users must be able to search for a sample by Barcode, Study, or Box Number and immediately see its Freezer Location.
* **REQ-RPT-02 (Audit Trail):** Every change to a sample record (location change, status change) must be logged with:
    * User ID
    * Timestamp
    * Previous Value
    * New Value
* **REQ-RPT-03 (Manifests):** The system must generate a Shipping Manifest (PDF/Excel) listing all samples included in a specific shipment.

---

## 4. Non-Functional Requirements

### 4.1 Usability
* **NFR-USE-01:** The interface should be optimized for barcode scanner input (cursor auto-focus to input fields).
* **NFR-USE-02:** Visual indicators (color coding) should be used to denote full boxes vs. empty slots in the Box View.

### 4.2 System Constraints
* **NFR-SYS-01:** The system will operate within a single facility (LAN/Intranet based).
* **NFR-SYS-02:** Data backups must be performed daily.

### 4.3 Performance
* **NFR-PER-01:** Bulk import of 1,000+ records should complete within 30 seconds.
* **NFR-PER-02:** Search results for a specific barcode should appear in under 2 seconds.

---

## 5. Data Entities (Conceptual)

### 5.1 Specimen Record
* **Identifiers:** Internal ID, External Barcode, Legacy ID.
* **Metadata:** Study Code, Sample Type, Collection Date.
* **Location:** Link to Box ID, Position (Row, Col).
* **Status:** In-Stock, Shipped, Missing, Depleted.
* **Filter Paper Specifics:** Remaining Spots (Integer 0-4).

### 5.2 Storage Container (Box)
* **Attributes:** Box Number/Name, Box Type (9x9, 10x10).
* **Location:** Link to Freezer ID, Shelf ID.

### 5.3 Shipment Event
* **Attributes:** Shipment Date, Destination, Courier, Tracking Number.
* **Content:** List of Specimen IDs included.

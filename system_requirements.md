# Requirements Specification: Specimen Tracking And Storage Information System (STASIS)

## 1. Introduction

### 1.1 Purpose

The purpose of the Specimen Tracking And Storage Information System (STASIS) is to manage the lifecycle of biological samples (specimens) from receipt through storage and final shipment. The system will act as the "Source of Truth" for the physical location of every sample in the facility.

### 1.2 Scope

- **Inbound:** Receiving samples from various study projects (e.g., Plasma, Buffy Coat, Filter Paper).
- **Storage:** Managing a hierarchical storage structure (Freezer → Rack → Box → Position).
- **Internal Movement:** Tracking the relocation of individual samples or entire boxes.
- **Outbound:** Processing shipment requests, retrieving samples, and logging shipment details.

---

## 2. Functional Requirements

### 2.1 Sample Accessioning (Intake)

The system must allow for the entry of new samples into the inventory.

- **REQ-ACC-01:** The system shall support the registration of individual samples via manual entry or barcode scanning.
- **REQ-ACC-02:** The system shall support **Bulk Import** (CSV/Excel/SQL Interface) to register large batches of samples arriving from different projects.
- **REQ-ACC-03:** Each specimen record must capture, at minimum:
    - Unique Barcode ID
    - Study/Project Name
    - Sample Type (e.g., Plasma, Filter Paper, Whole Blood)
    - Collection Date
- **REQ-ACC-04:** The system must validate that a Barcode ID is unique within the system to prevent duplicate records.

### 2.2 Storage & Inventory Management

The system must map the physical location of every sample.

- **REQ-INV-01 (Location Hierarchy):** The system must define storage locations using the following hierarchy:
    - `Freezer` (Name/ID)
    - `Shelf/Rack` (Location within Freezer)
    - `Box` (Container ID/Number)
    - `Position` (Row/Column or Numeric Slot 1-81/1-100)
- **REQ-INV-02 (Box Management):** Users must be able to assign a specific location (Freezer/Shelf) to a Box.
- **REQ-INV-03 (Sample Placement):** Users must be able to assign a specific coordinate (Row/Column) to a sample within a Box.
- **REQ-INV-04 (Conflict Detection):** The system must prevent placing a sample in a box position that is already occupied.

### 2.3 Specimen Movement

The system must track movement to maintain chain of custody.

- **REQ-MOV-01 (Individual Move):** The system shall allow users to move a single sample from "Box A" to "Box B".
- **REQ-MOV-02 (Bulk Move/Re-boxing):** The system shall allow users to scan all samples in a physical box to update their digital location to match the physical reality (e.g., after a box is spilled or samples are found in incorrect positions).
- **REQ-MOV-03 (Box Relocation):** The system shall allow users to move an entire Box from one Freezer/Rack to another without having to scan individual samples inside.
- **REQ-MOV-04 (Empty Box Logic):** If all samples are removed from a Box, or if a Box is physically removed from a Freezer, the system must automatically update the Freezer/Rack location of that Box to "None/Unassigned" to free up capacity.
- **REQ-MOV-05 (Box Populate):** The system shall allow samples to be scanned or added to an existing box while keeping the positions of the originally scanned samples intact. New samples shall be assigned to the next available position following the last occupied slot.
- **REQ-MOV-06 (Move to Temp):** The system shall provide a "Temp" holding location where samples can be temporarily placed when they cannot be deleted but need to be unassigned from their current box position. Samples in Temp can later be allocated to a proper box position when needed.
- **REQ-MOV-07 (Move to Trash/Discard):** The system shall support a discard workflow for samples that have been physically kept for an extended period and are no longer of value, in order to free freezer space for newly collected samples. Sample discard requires documented approval from the Executive Director (ED), Regulatory Department, and concerned Study PIs before execution.

### 2.4 Shipment Management

The system must manage the retrieval and dispatch of samples based on external requests.

#### 2.4.1 Approval Workflow

- **REQ-SHP-01 (Request Approval):** All sample shipment requests must be reviewed and approved by the Executive Director (ED) and/or Head of Regulatory before processing.

#### 2.4.2 Request Import & Availability

- **REQ-SHP-02 (Request Import):** The system shall allow users to import a "Shipment Request" (via Excel/CSV) containing:
    - External ID/Barcode
    - Requested Sample Type
    - Requestor Name
    - Request Date
- **REQ-SHP-03 (Availability Check):** Upon importing a request, the system must run a query to match requested items against current inventory.
- **REQ-SHP-04 (Availability Report Generation):** After processing a shipment request, the system shall generate an availability report showing:
    - Number of samples available
    - Samples that are lost/missing
    - Samples that have been previously shipped
    - Samples that have been discarded
    - Samples not yet received in Kampala
- **REQ-SHP-05 (Report Distribution):** The availability report shall be distributed to Regulatory, the Requestor, and the Project PIs to allow requestors to adjust their requests if insufficient samples are available.

#### 2.4.3 Status Tracking

- **REQ-SHP-06 (Status Workflow):** The system shall track the status of a requested sample. Valid statuses include:
    - `In Inventory` (Available)
    - `Staged` (Pulled from freezer, waiting to ship)
    - `Shipped` (Sent to destination)
    - `Not Found` (Requested but physically missing)
    - `Previously Shipped` (Requested but record shows it left already)
    - `Discarded` (Sample was disposed of)
    - `Not Yet Received` (Sample not yet in Kampala)

#### 2.4.4 Shipping Operations

- **REQ-SHP-07 (Shipping Container):** The system must record the new storage configuration for shipment (e.g., putting samples into a Shipping Box, specifying Row/Col within the shipping container).
- **REQ-SHP-08 (Ship Entire Box):** The system shall allow users to pull and ship an entire box of samples when requested, without requiring individual sample scanning.

### 2.5 Special Handling: Filter Papers

Specific logic is required for Filter Paper samples which may be used multiple times (punched) before being depleted.

- **REQ-SPL-01:** For sample type "Filter Paper," the system must track usage/remaining quantity.
- **REQ-SPL-02:** The system shall allow up to 4 distinct usages (cohort/request dates) per Filter Paper sample to track remaining "spots."
- **REQ-SPL-03:** A Filter Paper sample should only be marked as fully "Shipped/Depleted" when all spots are used or the entire card is sent.
- **REQ-SPL-04 (Filter Paper Shipping Restriction):** The system shall enforce that a maximum of 2 Filter Paper spots may be shipped to international collaborators. The remaining 2 spots must be reserved for local consumption.

### 2.6 Special Handling: Plasma

Specific logic is required for Plasma samples to ensure local retention of backup aliquots.

- **REQ-SPL-05 (Plasma Shipping Restriction):** The system shall enforce that only one Plasma aliquot (Plasma-1) may be shipped, with the second aliquot (Plasma-2) reserved for local consumption.
- **REQ-SPL-06 (Single Aliquot Exception):** Where only one plasma aliquot was collected from a study participant, the system shall require documented approval from the ED and/or Regulatory Department before that sample can be shipped.

---

## 3. Reporting & Auditing Requirements

- **REQ-RPT-01 (Search):** Users must be able to search for a sample by Barcode, Study, or Box Number and immediately see its Freezer Location. The search shall return comprehensive information about sample storage history and current status.
- **REQ-RPT-02 (Box Location):** The system shall be able to locate and display the exact physical location of a box, including Freezer, Compartment, and Rack position.
- **REQ-RPT-03 (Audit Trail):** Every change to a sample record (location change, status change) must be logged with:
    - User ID
    - Timestamp
    - Previous Value
    - New Value
- **REQ-RPT-04 (Manifests):** The system must generate a Shipping Manifest (PDF/Excel) listing all samples included in a specific shipment.

### 3.1 User Access Rights

- **REQ-SEC-01 (Role-Based Access):** The system shall implement role-based access control, granting different users different access rights based on their responsibilities.
- **REQ-SEC-02 (User Roles):** The system shall support the following user roles:
    - **Read:** View-only access to sample records and reports
    - **Write:** Ability to add, move, and update sample records
    - **Admin:** Full system access including user management, configuration, and approval workflows

---

## 4. Non-Functional Requirements

### 4.1 Usability

- **NFR-USE-01:** The interface should be optimized for barcode scanner input (cursor auto-focus to input fields).
- **NFR-USE-02:** Visual indicators (color coding) should be used to denote full boxes vs. empty slots in the Box View.

### 4.2 System Constraints

- **NFR-SYS-01:** The system will operate within a single facility (LAN/Intranet based).
- **NFR-SYS-02:** Data backups must be performed daily.

### 4.3 Performance

- **NFR-PER-01:** Bulk import of 1,000+ records should complete within 30 seconds.
- **NFR-PER-02:** Search results for a specific barcode should appear in under 2 seconds.

---

## 5. Data Entities (Conceptual)

### 5.1 Specimen Record

- **Identifiers:** Internal ID, External Barcode, Legacy ID.
- **Metadata:** Study Code, Sample Type, Collection Date.
- **Location:** Link to Box ID, Position (Row, Col).
- **Status:** In-Stock, Shipped, Missing, Depleted, Discarded, Temp, Not Yet Received.
- **Filter Paper Specifics:** Remaining Spots (Integer 0-4), Spots Shipped (Integer 0-2), Spots Reserved Local (Integer 0-2).
- **Plasma Specifics:** Aliquot Number (1 or 2), Shipping Eligibility Flag.

### 5.2 Storage Container (Box)

- **Attributes:** Box Number/Name, Box Type (9x9, 10x10).
- **Location:** Link to Freezer ID, Shelf ID.
- **Special Types:** Standard Box, Shipping Box, Temp Box, Trash Box.

### 5.3 Shipment Event

- **Attributes:** Shipment Date, Destination, Courier, Tracking Number.
- **Content:** List of Specimen IDs included.
- **Approval:** Approver Name, Approval Date, Approval Status.

### 5.4 User

- **Attributes:** User ID, Username, Full Name, Email.
- **Access:** Role (Read/Write/Admin), Active Status.
- **Audit:** Created Date, Last Login.

---

## 6. Appendix: Requirements Traceability

| Requirement ID | Category | Description | Priority |
|----------------|----------|-------------|----------|
| REQ-ACC-01 | Accessioning | Individual sample registration | High |
| REQ-ACC-02 | Accessioning | Bulk import | High |
| REQ-ACC-03 | Accessioning | Minimum data fields | High |
| REQ-ACC-04 | Accessioning | Barcode uniqueness validation | High |
| REQ-INV-01 | Inventory | Location hierarchy | High |
| REQ-INV-02 | Inventory | Box management | High |
| REQ-INV-03 | Inventory | Sample placement | High |
| REQ-INV-04 | Inventory | Conflict detection | High |
| REQ-MOV-01 | Movement | Individual move | High |
| REQ-MOV-02 | Movement | Bulk move/re-boxing | High |
| REQ-MOV-03 | Movement | Box relocation | Medium |
| REQ-MOV-04 | Movement | Empty box logic | Medium |
| REQ-MOV-05 | Movement | Box populate (incremental add) | Medium |
| REQ-MOV-06 | Movement | Move to Temp | Medium |
| REQ-MOV-07 | Movement | Move to Trash/Discard | Medium |
| REQ-SHP-01 | Shipment | Request approval workflow | High |
| REQ-SHP-02 | Shipment | Request import | High |
| REQ-SHP-03 | Shipment | Availability check | High |
| REQ-SHP-04 | Shipment | Availability report generation | Medium |
| REQ-SHP-05 | Shipment | Report distribution | Medium |
| REQ-SHP-06 | Shipment | Status workflow | High |
| REQ-SHP-07 | Shipment | Shipping container configuration | High |
| REQ-SHP-08 | Shipment | Ship entire box | Medium |
| REQ-SPL-01 | Special Handling | Filter paper usage tracking | High |
| REQ-SPL-02 | Special Handling | Filter paper 4-spot tracking | High |
| REQ-SPL-03 | Special Handling | Filter paper depletion logic | High |
| REQ-SPL-04 | Special Handling | Filter paper shipping restriction | High |
| REQ-SPL-05 | Special Handling | Plasma shipping restriction | High |
| REQ-SPL-06 | Special Handling | Single aliquot exception | Medium |
| REQ-RPT-01 | Reporting | Search functionality | High |
| REQ-RPT-02 | Reporting | Box location lookup | Medium |
| REQ-RPT-03 | Reporting | Audit trail | High |
| REQ-RPT-04 | Reporting | Shipping manifests | High |
| REQ-SEC-01 | Security | Role-based access control | High |
| REQ-SEC-02 | Security | User roles definition | High |
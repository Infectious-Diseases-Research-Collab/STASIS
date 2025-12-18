# STASIS Implementation Plan

## Document Information

| Field | Value |
|-------|-------|
| Project | Specimen Tracking And Storage Information System (STASIS) |
| Related Document | system_requirements_updated.md |
| Created | December 2024 |
| Status | Draft |
| Owner | |

---

## Overview

This document outlines the implementation order for STASIS requirements. Requirements are organized into phases based on technical dependencies - some features cannot be built until foundational components exist.

### How to Use This Document

1. Work through phases sequentially - Phase 2 cannot begin until Phase 1 is complete
2. Within each phase, items can often be parallelized across team members
3. Update the Assignee, Status, and Target Date columns as work progresses
4. Reference requirement IDs to trace back to full specifications in the requirements document

### Status Key

| Status | Meaning |
|--------|---------|
| Not Started | Work has not begun |
| In Progress | Currently being developed |
| In Review | Development complete, under review/testing |
| Complete | Deployed and verified |
| Blocked | Cannot proceed - see notes |

---

## Phase 1: Foundation

**Phase Goal:** Establish the core data structures and security model that all other features depend on.

**Estimated Duration:** _____ weeks

**Phase Dependencies:** None - this is the starting point

| Order | Req ID | Description | Assignee | Status | Target Date | Notes |
|-------|--------|-------------|----------|--------|-------------|-------|
| 1.1 | REQ-SEC-01, REQ-SEC-02 | User roles and access control (Read/Write/Admin) | | Not Started | | Must be first - all operations need authenticated users |
| 1.2 | REQ-INV-01 | Location hierarchy (Freezer → Rack → Box → Position) | | Not Started | | Core data structure for all storage |
| 1.3 | REQ-ACC-03, REQ-ACC-04 | Specimen data model and barcode uniqueness validation | | Not Started | | Defines the central entity |
| 1.4 | REQ-INV-02 | Box management - assign boxes to freezer locations | | Not Started | | Depends on 1.2 |
| 1.5 | REQ-RPT-03 | Audit trail logging | | Not Started | | Build in from day one - retrofitting is costly |

**Phase 1 Completion Criteria:**
- [ ] Users can log in with assigned roles
- [ ] Freezer/Rack/Box/Position hierarchy is defined in database
- [ ] Specimen table exists with all required fields
- [ ] Boxes can be created and assigned to freezer locations
- [ ] All data changes are logged with user, timestamp, old value, new value

---

## Phase 2: Basic Operations

**Phase Goal:** Enable core sample entry, placement, and lookup functionality.

**Estimated Duration:** _____ weeks

**Phase Dependencies:** Phase 1 must be complete

| Order | Req ID | Description | Assignee | Status | Target Date | Notes |
|-------|--------|-------------|----------|--------|-------------|-------|
| 2.1 | REQ-ACC-01 | Individual sample registration (manual entry + barcode scan) | | Not Started | | First way to get samples into system |
| 2.2 | REQ-INV-03 | Sample placement - assign samples to box positions | | Not Started | | Depends on 2.1 |
| 2.3 | REQ-INV-04 | Conflict detection - prevent double-booking positions | | Not Started | | Depends on 2.2 |
| 2.4 | REQ-RPT-01 | Search by barcode, study, or box number | | Not Started | | Depends on 2.1 |
| 2.5 | REQ-RPT-02 | Box location lookup | | Not Started | | Depends on Phase 1.4 |
| 2.6 | REQ-ACC-02 | Bulk import (CSV/Excel) | | Not Started | | Depends on 2.1, 2.2, 2.3 - do after single entry works |

**Phase 2 Completion Criteria:**
- [ ] Users can register individual samples via UI and barcode scanner
- [ ] Samples can be assigned to specific box positions
- [ ] System rejects attempts to place sample in occupied position
- [ ] Users can search and find samples with location displayed
- [ ] Users can look up where a specific box is located
- [ ] Bulk import successfully processes CSV/Excel files with validation

---

## Phase 3: Movement Operations

**Phase Goal:** Enable relocation of samples and boxes, including special handling locations.

**Estimated Duration:** _____ weeks

**Phase Dependencies:** Phase 2 must be complete

| Order | Req ID | Description | Assignee | Status | Target Date | Notes |
|-------|--------|-------------|----------|--------|-------------|-------|
| 3.1 | REQ-MOV-01 | Individual move - move one sample between boxes | | Not Started | | Foundation for all movement |
| 3.2 | REQ-MOV-03 | Box relocation - move entire box to different freezer/rack | | Not Started | | Independent of sample-level moves |
| 3.3 | REQ-MOV-02 | Bulk move/re-boxing - rescan box to fix discrepancies | | Not Started | | Depends on 3.1 |
| 3.4 | REQ-MOV-04 | Empty box logic - auto-unassign empty boxes | | Not Started | | Depends on 3.1, 3.3 |
| 3.5 | REQ-MOV-05 | Box populate - add samples to existing box incrementally | | Not Started | | Depends on 3.1 |
| 3.6 | REQ-MOV-06 | Move to Temp - temporary holding location | | Not Started | | Depends on 3.1 |
| 3.7 | REQ-MOV-07 | Move to Trash/Discard - disposal workflow with approvals | | Not Started | | Depends on 3.1 and roles (1.1) |

**Phase 3 Completion Criteria:**
- [ ] Individual samples can be moved between boxes with audit trail
- [ ] Entire boxes can be relocated to different freezer/rack positions
- [ ] Re-scanning a box updates all sample positions to match physical reality
- [ ] Empty boxes are automatically marked as unassigned
- [ ] Samples can be added to partially-filled boxes
- [ ] Samples can be moved to Temp holding location
- [ ] Discard workflow requires and records ED/Regulatory/PI approval

---

## Phase 4: Shipment Management

**Phase Goal:** Enable end-to-end shipment request processing, from import through dispatch.

**Estimated Duration:** _____ weeks

**Phase Dependencies:** Phase 2 must be complete; can run in parallel with Phase 3

| Order | Req ID | Description | Assignee | Status | Target Date | Notes |
|-------|--------|-------------|----------|--------|-------------|-------|
| 4.1 | REQ-SHP-02 | Request import - load shipment requests from CSV/Excel | | Not Started | | Entry point for shipment workflow |
| 4.2 | REQ-SHP-03 | Availability check - match requests against inventory | | Not Started | | Depends on 4.1 and search (2.4) |
| 4.3 | REQ-SHP-06 | Status workflow - track sample through shipment stages | | Not Started | | Depends on 4.2 |
| 4.4 | REQ-SHP-01 | Request approval - ED/Regulatory approval workflow | | Not Started | | Depends on 4.1 and roles (1.1) |
| 4.5 | REQ-SHP-04 | Availability report generation | | Not Started | | Depends on 4.2 |
| 4.6 | REQ-SHP-05 | Report distribution to stakeholders | | Not Started | | Depends on 4.5 |
| 4.7 | REQ-SHP-07 | Shipping container configuration | | Not Started | | Depends on 4.3 |
| 4.8 | REQ-SHP-08 | Ship entire box | | Not Started | | Depends on 4.7 and box relocation (3.2) |
| 4.9 | REQ-RPT-04 | Shipping manifest generation (PDF/Excel) | | Not Started | | Depends on 4.7 |

**Phase 4 Completion Criteria:**
- [ ] Shipment requests can be imported from CSV/Excel
- [ ] System matches requests against inventory and identifies availability
- [ ] Sample status tracks through: In Inventory → Staged → Shipped
- [ ] Shipment requests require ED/Regulatory approval before processing
- [ ] Availability reports are generated showing available/missing/shipped/discarded
- [ ] Reports can be distributed to Regulatory, Requestor, and PIs
- [ ] Shipping box configuration (positions) can be recorded
- [ ] Entire boxes can be shipped as a unit
- [ ] Shipping manifests can be generated as PDF or Excel

---

## Phase 5: Special Handling Rules

**Phase Goal:** Implement business rules specific to Filter Paper and Plasma sample types.

**Estimated Duration:** _____ weeks

**Phase Dependencies:** Phase 2 for Filter Paper basics; Phase 4 for shipping restrictions

| Order | Req ID | Description | Assignee | Status | Target Date | Notes |
|-------|--------|-------------|----------|--------|-------------|-------|
| 5.1 | REQ-SPL-01 | Filter Paper usage tracking | | Not Started | | Extends specimen data model |
| 5.2 | REQ-SPL-02 | Filter Paper 4-spot tracking across requests | | Not Started | | Depends on 5.1 |
| 5.3 | REQ-SPL-03 | Filter Paper depletion logic | | Not Started | | Depends on 5.2 |
| 5.4 | REQ-SPL-04 | Filter Paper shipping restriction (2 international / 2 local) | | Not Started | | Depends on 5.2 and Phase 4 |
| 5.5 | REQ-SPL-05 | Plasma shipping restriction (Plasma-1 only) | | Not Started | | Depends on Phase 4 |
| 5.6 | REQ-SPL-06 | Single aliquot exception approval workflow | | Not Started | | Depends on 5.5 and approval workflow (4.4) |

**Phase 5 Completion Criteria:**
- [ ] Filter Paper samples track remaining spots (0-4)
- [ ] Usage is recorded per request/cohort
- [ ] Filter Papers auto-mark as depleted when all spots used
- [ ] System enforces max 2 spots for international shipment
- [ ] System enforces only Plasma-1 can be shipped
- [ ] Single-aliquot plasma requires and records ED/Regulatory approval

---

## Phase 6: Non-Functional & Polish

**Phase Goal:** Optimize performance, usability, and infrastructure.

**Estimated Duration:** _____ weeks

**Phase Dependencies:** Various - see individual items

| Order | Req ID | Description | Assignee | Status | Target Date | Notes |
|-------|--------|-------------|----------|--------|-------------|-------|
| 6.1 | NFR-USE-01 | Barcode scanner optimization (auto-focus) | | Not Started | | After core UI exists (Phases 2-3) |
| 6.2 | NFR-USE-02 | Visual indicators / color coding for box view | | Not Started | | After sample placement (2.2) |
| 6.3 | NFR-PER-01 | Bulk import performance (1000+ in 30 sec) | | Not Started | | After bulk import works (2.6) |
| 6.4 | NFR-PER-02 | Search performance (< 2 seconds) | | Not Started | | After search works (2.4) |
| 6.5 | NFR-SYS-02 | Daily backup configuration | | Not Started | | Once database structure is stable |

**Phase 6 Completion Criteria:**
- [ ] Cursor auto-focuses to barcode input fields
- [ ] Box view shows color-coded occupied vs empty positions
- [ ] Bulk import of 1000+ records completes in under 30 seconds
- [ ] Barcode search returns results in under 2 seconds
- [ ] Automated daily backups are configured and verified

---

## Team Assignments

| Team Member | Role | Assigned Phases/Items |
|-------------|------|----------------------|
| | | |
| | | |
| | | |
| | | |

---

## Parallelization Opportunities

The following work streams can proceed in parallel once their dependencies are met:

| Stream | Items | Can Start After |
|--------|-------|-----------------|
| Stream A: Data/Backend | Phase 1 → Phase 5 | Immediately |
| Stream B: Core Sample Ops | Phase 2 → Phase 3 | Phase 1 complete |
| Stream C: Shipment Workflow | Phase 4 | Phase 2 complete |
| Stream D: UI/UX Polish | Phase 6 | Relevant features complete |

---

## Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Phase 1 delays cascade to all other phases | High | Medium | Prioritize Phase 1; consider additional resources |
| Bulk import performance issues | Medium | Medium | Design for performance from start; test with realistic data volumes |
| Approval workflow complexity | Medium | Low | Clarify business rules with ED/Regulatory early |
| | | | |

---

## Change Log

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| | 1.0 | | Initial draft |
| | | | |

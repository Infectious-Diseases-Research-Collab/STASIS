# STASIS Deferred & Pending Items

**Last Updated:** November 5, 2026
**Purpose:** Track all items across all phases that were partially implemented or deferred. Use this as a checklist when returning to unfinished work.

---

## Partially Implemented

These items have groundwork in place but need additional work to be fully functional.

### 1. Email Distribution of Availability Report (Phase 4, item 4.4)

- **What exists:** `EmailSender` service is registered; availability report renders on-screen after CSV import and in batch detail view.
- **What's missing:** No code connects `EmailSender` to the shipment workflow. After import, the system should email the availability report to Regulatory, the Requestor, and PIs.
- **Where to implement:** `ShipmentService.cs` (after `ImportBatchFromCsvAsync`) or a new handler in `Shipments/Create.cshtml.cs`.
- **Depends on:** A working SMTP configuration in `appsettings.json` or user secrets.
- **Study access:** The emailed report must respect study-scoped access — leave out rows marked `No Access`, and don't send a recipient rows for studies they can't view (`IStudyAccessService`).

### 2. Whole-Box Shipping UI (Phase 4, item 4.8)

- **What exists:** `Shipment` model has `IsEntireBox` (bool) and `ShippedBoxID` (int?) fields. The database columns are ready.
- **What's missing:** No UI to select "ship entire box" during the Ship action. The Ship form only handles individual specimen shipping from matched requests.
- **Where to implement:** `Pages/Shipments/History.cshtml` (Ship form section) and `ShipmentService.ShipBatchAsync`.
- **Behavior:** When shipping an entire box, all specimens in that box should be marked Shipped, and `IsEntireBox = true` / `ShippedBoxID` should be set on the Shipment record.
- **Study access:** Require `CanEditAsync` on the box's `StudyID`, and refuse boxes with a NULL `StudyID` that hold more than one study's specimens — the same rule Move and Rebox enforce.

### 3. Staging Status for Specimens (Phase 4, item 4.6)

- **What exists:** Specimens go directly from their current status to "Shipped" when the Ship action is executed.
- **What's missing:** No intermediate "Staged" status.
- **Where to implement:** `ShipmentService.cs` — add a `StageBatchAsync` method that sets matched specimens to Status="Staged" after approval; the Ship action then transitions from Staged to Shipped.

### 4. Single Aliquot Exception (Phase 6, item 6.6)

- **What exists:** Approval model supports `ApprovalType = "SingleAliquotException"` (DB check constraint allows it). Approval engine is implemented (Phase 5). Plasma Aliquot-2 is blocked from outbound shipment (Phase 6).
- **What's missing:** No validation to detect when a Plasma specimen is the **last remaining aliquot** and require a SingleAliquotException approval before shipping.
- **Where to implement:** `ShipmentService.ValidateShipmentAsync` — query for sibling aliquots; if no other aliquot is In-Stock, require exception approval.


### 6. Batch Add — Rich Success Summary (Batch Entry feature)

- **What exists:** After a successful batch save, the success summary shows a bulleted list of created barcodes.
- **What's missing:** The design spec calls for a table listing each created specimen with Barcode, Type, Box, and Position columns.
- **Where to implement:** `Pages/Samples/Add.cshtml.cs` — expand `TempData["CreatedBarcodes"]` to `TempData["CreatedSpecimens"]` (JSON array of `{barcode, type, box, position}`), and update the summary table in `Add.cshtml` to render those columns.

### 7. Filter Paper Position Uniqueness Gap (Batch Entry feature)

- **What exists:** A `UNIQUE ("BoxID", "PositionRow", "PositionCol")` index on `tbl_Specimens`. In-batch position conflict check in `AddModel.OnPostAsync`.
- **What's missing:** PostgreSQL treats `NULL` values as distinct in unique constraints, so two Filter Paper specimens with `(BoxID=X, PositionRow=Y, PositionCol=NULL)` do not violate the constraint. The in-batch check catches same-request conflicts, but concurrent requests or manual DB inserts can produce duplicate physical slots.
- **Where to implement:** Add a partial unique index scoped to `WHERE "PositionCol" IS NULL` (e.g., `UNIQUE ("BoxID", "PositionRow") WHERE "PositionCol" IS NULL`) as a separate EF migration and SQL script entry. Requires testing against the existing Filter Paper unique-position behaviour.

### 8. GetOccupiedPositions drops Filter Paper positions

- **What exists:** `SampleService.GetOccupiedPositions` filters `WHERE PositionCol != null`, silently omitting all Filter Paper Binder specimens (col is always null).
- **Impact:** The handler `OnGetOccupiedPositionsAsync` is currently unused by the Add page JS, so no runtime failure today. Any future caller relying on it for FP Binder boxes will receive an empty set.
- **Fix:** Remove the `PositionCol != null` predicate and change the return type to `List<(int Row, int? Col)>`.

### 9. Study-scoped Visit Types (Add Sample page)

- **Box half: resolved (2026-10-22).** `Box` now has a `StudyID` column (added by the study-scoped access control work), and `Pages/Samples/Add.cshtml.cs`/`.cshtml` now filter the Box dropdown to boxes already claimed by the selected study plus genuinely unassigned ones, refreshed via a new `OnGetBoxesForStudyAsync` AJAX handler when the study selection changes. Boxes belonging to a different, non-empty study are correctly excluded. Mixing specimens from different studies into one box is also now blocked (`IStorageService.ValidateAndAssignBoxStudyAsync`), closing the underlying data-integrity bug this item originally flagged.
- **Visit Type half: still open.** `VisitType` has no `StudyID` column — visit types are one global list shared by every study, and all of them appear in the Add Sample dropdown regardless of which study is selected. This is not an access leak (visit types are reference data, not study data). As of 2026-11-05 the dev database has no visit types and no specimen references one, so existing data can't settle the open question below.
- **What's missing:** When a study is selected, the Visit Type dropdown should be filtered to only show visit types relevant to that study.
- **Agreed approach:** Schema change — add a `StudyID` FK to `tbl_VisitTypes`, configure the association in `LabSetup/VisitTypes`, then filter dynamically on the Add Sample page via an AJAX call when the study changes (mirroring the pattern now used for boxes).
- **Open question before implementing:** does a visit type belong to one study only, or can the same visit type (e.g. "Baseline") be shared across multiple studies?
- **Where to implement:** EF migration to add `StudyID` FK to `tbl_VisitTypes`, update `LabSetup/VisitTypes` to associate records with a study, add an `OnGetVisitTypesForStudyAsync` AJAX handler to `Add.cshtml.cs` (same shape as the new `OnGetBoxesForStudyAsync`), update `Add.cshtml` JS to re-filter the Visit Type dropdown on study change.


---

## Resolved Items

| Item | Phase | Resolution |
|------|-------|------------|
| Box location lookup from Samples list | Phase 6 | Box labels and barcodes now link to Search and Detail pages |
| Audit trail coverage gaps | Phase 5 | All services and user management now log audit entries |
| Phase 6 special sample rules | Phase 6 | Filter paper tracking, Plasma-2 block, spot limits all implemented |
| Barcode scanner autofocus | Phase 7 | All input fields have `autofocus` |
| Color-coded box grid | Phase 3/7 | Implemented and verified |
| Search performance indexes | Phase 7 | Migration `AddSearchPerformanceIndexes` adds indexes on Status, StudyID, SampleTypeID |
| Backup/restore procedures | Phase 7 | Documented in `STASIS_Test_Plan.md` |
| Deployment checklist | Phase 7 | Documented in `STASIS_Test_Plan.md` |
| Automated tests (was item 5) | Phase 7 | `STASIS.Tests` xUnit project added with the study-scoped access control work (unit, service, migration, integration and IDOR tests); see README "Running the tests" |
| Filter Paper Binder rendered as a 9×9 grid on Boxes/Search (was item 10) | — | Binders now render as a numbered linear slot list keyed on `PositionRow` (100 slots, growing for legacy binders past 100); specimen list shows "Slot N" |
| "View" button on Boxes/Search appeared to do nothing (was item 11) | — | It worked, but the detail section rendered below the results list without scrolling; the View link now jumps to `#box-detail` |
| "Forgot password" returned a 500 and revealed which emails had accounts; admin reset hid its errors and skipped the forced change for users without a profile (was item 12) | — | Decided on admin-only resets (2026-11-05). Forgot/Reset Password pages now return 404, the login page says to contact a STASIS administrator, and the Edit User reset shows success/error banners and always sets `MustChangePassword`. If self-service resets are wanted later, set up SMTP with item 1 and make Forgot Password show the same message whether or not the account exists |

---

## Manual Testing Required

These items require manual verification and are documented in `STASIS_Test_Plan.md`:

- [ ] Barcode scanner testing with physical scanner hardware
- [ ] Box occupancy grid visual verification
- [ ] Bulk import performance with 1000+ row CSV
- [ ] Search performance benchmarking with 1000+ records
- [ ] Backup script execution and restore verification
- [ ] Full deployment walkthrough

---

## Quick Reference: Priority Order

If returning to this list, the recommended order is:

1. **Single aliquot exception** (item 4) — governance-critical edge case
2. **Email distribution** (item 1) — depends on SMTP setup
3. **Whole-box shipping** (item 2) — nice-to-have refinement
4. **Staging status** (item 3) — workflow refinement
5. **Manual testing** — run through checklists in `STASIS_Test_Plan.md`

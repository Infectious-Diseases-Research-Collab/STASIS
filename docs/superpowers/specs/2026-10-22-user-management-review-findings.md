# Code Review Findings: review/user-management (pre-PR)

Deep review of branch `review/user-management` against `main`, plus live verification against a
running instance on the dev database. Findings are ordered by severity; each must be fixed (or
explicitly deferred with a tracked item) before this branch is opened as a PR.

## 1. [CRITICAL] Boxes/Place lost its authorization gate entirely

**File:** `Pages/Boxes/Place.cshtml.cs`

The class-level `[Authorize(Roles = "Write,Admin")]` that used to gate this whole page was removed
when the global "Write" role was retired, and was never replaced on `OnGetAsync`. `OnPostAsync`
(box relocation) got an "editor anywhere" gate but never checks that the specific box being moved
belongs to a study the caller can edit.

**Live-verified** against a running build on the dev DB:
- A brand-new user with zero `UserStudyAccess` rows issued `GET /Boxes/Place?BoxLabel=QA-BOX-1`
  and received full specimen/rack/freezer detail for a box belonging to a study they have no
  access to.
- A user with Editor access to Study 2 only was able to `POST` to `OnPostAsync` and successfully
  relocate (change `RackID` of) a box belonging to Study 20, with the database write confirmed.

This is both a cross-study read leak and a cross-study write IDOR. In a biorepository, letting an
unrelated study's Editor physically relocate someone else's box risks real specimen mix-ups.

## 2. [HIGH] MoveToTempAsync makes its own specimen invisible

**File:** `STASIS/Services/StorageService.cs`, `MoveToTempAsync` (~line 216)

Unlike `MoveSpecimenAsync` and `ReboxSpecimensAsync`, `MoveToTempAsync` never calls
`ValidateAndAssignBoxStudyAsync`. The shared Temp box therefore keeps `StudyID == null` after
receiving a specimen. `ApplyStudyVisibility` (~line 122) only shows a null-`StudyID` box when it
has zero specimens, so once occupied it matches neither visibility branch and disappears from
Boxes/Search and all box-listing queries for every non-System-Admin user — including the person
who just moved their own specimen there.

## 3. [HIGH] Samples/Add leaks another study's box inventory on a rejected request

**File:** `Pages/Samples/Add.cshtml.cs`, `OnPostAsync` (~line 98-116)

`OnPostAsync` calls `LoadDropdownsAsync(userId, isSystemAdmin, StudyID)` using the posted,
not-yet-authorized `StudyID` *before* the `CanEditAsync` re-check. `LoadDropdownsAsync` uses that
`StudyID` to query real box/freezer/rack data into `AllBoxes`, which the view renders regardless
of whether the request is later rejected by `ModelState` or by the `CanEditAsync` failure — both
paths `return Page()` after the leak has already happened.

## 4. [MEDIUM] Discard's confirm step reveals specimen existence it shouldn't

**File:** `Pages/Samples/Discard.cshtml.cs`, `OnPostRequestAsync` (~line 88-126)

`OnPostLookupAsync` filters every match through `CanViewAsync` before showing anything, explicitly
to avoid revealing that a specimen exists in a study the caller can't view. `OnPostRequestAsync`
skips that gate and goes straight to `CanEditAsync`, so a caller who posts a barcode from a study
they can't even view gets back a message naming the specimen's `StudyCode` and confirming it
exists.

## 5. [MEDIUM] "Last Study Admin" guardrail lives in the page, not the service

**File:** `STASIS/Services/StudyAccessService.cs`, `RevokeAccessAsync` / `ChangeRoleAsync`

The rule "a study must always have at least one Study Admin" is enforced only by inline checks in
`Pages/Administration/EditUser.cshtml.cs`'s two POST handlers. `RevokeAccessAsync` and
`ChangeRoleAsync` themselves have no such check — their doc comments say callers are responsible
for it. Any future caller (an API, a bulk-admin script, a second admin page) silently bypasses an
invariant the design doc calls absolute.

## 6. [MEDIUM] Ambiguous legacy boxes are treated inconsistently between read and write paths

**Files:** `STASIS/Services/StorageService.cs` (`ValidateAndAssignBoxStudyAsync`),
`Pages/Samples/Add.cshtml.cs` (`CanAccessBoxForAddAsync`)

A box left with `StudyID == null` because its specimens already span more than one study (exactly
what the `BackfillBoxStudyId` migration leaves for manual admin cleanup) is correctly hidden from
non-admins on the read side (`ApplyStudyVisibility`). On the write side, neither
`ValidateAndAssignBoxStudyAsync` nor `CanAccessBoxForAddAsync` inspects the box's existing
specimens when `StudyID` is null — both treat it as freely claimable/accessible, defeating the
one-study-per-box invariant for the exact case flagged as needing manual review.

## 7. [MEDIUM] Barcode/study migration has no backfill or guard for legacy null StudyID rows

**File:** `Migrations/20260509184420_BarcodeStudyCompositeKey.cs`

This migration makes `tbl_Specimens.StudyID` `NOT NULL` with `defaultValue: 0` and adds an FK to
`tbl_Studies`, with no explicit handling of pre-existing `StudyID IS NULL` rows (legal under the
old nullable column). EF's generated SQL for this transition coerces existing NULLs to the
default (`0`) before applying the `NOT NULL` constraint; the subsequent `AddForeignKey` step would
then fail against any such row (no `Study` row with id `0` exists), aborting the migration with a
generic FK-violation error instead of a clear, actionable message. The current dev database has
zero such rows, but unlike the later `Box.StudyID` backfill (which got its own dedicated migration
and tests), this migration has no safety net.

## 8. [LOW-MEDIUM] Shipments/Create silently regressed instead of becoming study-scoped

**File:** `Pages/Shipments/Create.cshtml.cs`, line 10

`[Authorize(Roles = "Write,Admin")]` was narrowed to `[Authorize(Roles = "Admin")]`, not replaced
by a study-scoped check — contradicting the branch's own design doc, which lists this page as one
meant to get the same treatment as Boxes/Samples/Administration. Every per-study Editor who
previously held the global Write role loses the ability to create shipments, with no study-scoped
path provided.

## 9. [cleanup] Caller-authorization loop triplicated instead of reusing GetManageableStudiesAsync

**Files:** `Pages/Administration/Users.cshtml.cs`, `Pages/Administration/CreateUser.cshtml.cs`
(`AuthorizeCallerAsync`), `Pages/Administration/EditUser.cshtml.cs` (`AuthorizeCallerAsync`)

All three independently loop `GetVisibleStudyIdsAsync` + per-study `GetRoleAsync(...) ==
StudyAdmin` to build the same `managedStudyIds` set that `IStudyAccessService.GetManageableStudiesAsync`
already computes in one query — and that method is already called later in the same files for
dropdown population. Byte-for-byte identical loops in three files is a drift risk.

## 10. [efficiency] EditUser double-scans the whole UserStudyAccess table per page load

**File:** `Pages/Administration/EditUser.cshtml.cs`, `OnGetAsync`

`LoadAccessRowsAsync` and `BuildGrantableStudySelectListAsync` each independently call
`GetAllAccessRowsAsync()`, which fetches and joins every `UserStudyAccess` row in the system. A
single GET therefore issues two full-table scans instead of one.

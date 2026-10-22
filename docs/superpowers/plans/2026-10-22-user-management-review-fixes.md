# User-Management Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the authorization gaps and data-integrity bugs found in the pre-PR review of
`review/user-management`, without changing any already-correct study-scoped behavior.

**Architecture:** Every fix reuses the existing `IStudyAccessService` / `IStorageService`
patterns already established elsewhere on this branch (resolve `visibleStudyIds` or call
`CanEditAsync`/`CanViewAsync` in the page model, pass the result into the service) rather than
introducing new abstractions. No new services, no new DI registrations.

**Tech Stack:** ASP.NET Core Razor Pages (.NET 10), EF Core + Npgsql, xUnit, Postgres 17.

**Spec:** `docs/superpowers/specs/2026-10-22-user-management-review-findings.md` (10 findings;
this plan has one task per finding, same numbering).

## Global Constraints

- Never call `StasisDbContext` directly from a page model — go through `IStorageService`,
  `ISampleService`, or `IStudyAccessService` (CLAUDE.md).
- 4-space indentation; PascalCase types, camelCase locals/params, `I`-prefixed interfaces
  (CLAUDE.md).
- Commit messages: short, sentence-style, past tense (CLAUDE.md).
- Do not change the public signature of any `IStudyAccessService`/`IStorageService` method unless
  a task below explicitly says to — other call sites on this branch depend on the current
  signatures and are out of scope for this plan.
- Every new/changed authorization check must have a passing regression test before being
  considered done — this is a security-fix plan; untested is unfixed.

## Review Focus

- A caller who is Editor on study A but has **zero** access to study B must get `Forbid()` (or
  an equivalent silent rejection) from every write path touching a study-B box, not just the ones
  already covered by existing tests — Task 1's new tests target exactly this gap.
- A box with `StudyID == null` and **zero** specimens (genuinely unclaimed) must stay claimable —
  Task 6's fix must not regress the existing "claim an empty box" tests in
  `BoxStudyEnforcementTests.cs`.
- A System Admin must still bypass every per-study check unchanged — every task below must keep
  the `IsSystemAdminAsync` short-circuit in whichever method it touches.
- Revoking/demoting a Study Admin when **another** Study Admin remains on the same study must
  still succeed — Task 5's fix must not accidentally make the guardrail stricter than before.
- A rejected `Samples/Add` POST (wrong `StudyID`) must still redisplay the caller's *own*
  editable studies and boxes correctly — Task 3's fix must not break the authorized-caller path,
  only the leak on the unauthorized one.

---

## Task 1: Boxes/Place — restore per-request and per-box authorization

**Files:**
- Modify: `Pages/Boxes/Place.cshtml.cs`
- Test: `STASIS.Tests/Integration/RemainingWriteGatesTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.IsSystemAdminAsync(string)`,
  `IStudyAccessService.GetVisibleStudyIdsAsync(string)`,
  `IStudyAccessService.CanEditAsync(string, int)`, `IStorageService.GetBoxByLabelAsync(string,
  IReadOnlyCollection<int>?)`, `IStorageService.GetBoxWithSpecimens(int,
  IReadOnlyCollection<int>?)` (all already exist, no signature changes).
- Produces: nothing new consumed elsewhere.

- [ ] **Step 1: Write the failing tests** in `RemainingWriteGatesTests.cs` (same file, same
  `BuildPlaceModel`/`AddBoxAsync`/`GrantAsync`/`CreateUserAsync`/`CreateSystemAdminAsync` helpers
  already defined there):

```csharp
[Fact]
public async Task Place_OnGetAsync_CallerWithNoAccessToBoxStudy_DoesNotReturnBox()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg19@test.com");
    var study = new Study { StudyCode = "RWG-P-GET-OTHER" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    // Caller has zero UserStudyAccess rows at all.
    var caller = await CreateUserAsync(services, "noaccess-rwg19@test.com");

    var box = await AddBoxAsync(context, "RWG-P-GETBOX1", study.StudyID);

    var model = BuildPlaceModel(services, caller.Id);
    model.BoxLabel = box.BoxLabel;

    await model.OnGetAsync();

    Assert.Null(model.FoundBox);
}

[Fact]
public async Task Place_OnGetAsync_CallerWithEditAccessToBoxStudy_ReturnsBox()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg20@test.com");
    var study = new Study { StudyCode = "RWG-P-GET-MINE" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    var caller = await CreateUserAsync(services, "editor-rwg20@test.com");
    await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

    var box = await AddBoxAsync(context, "RWG-P-GETBOX2", study.StudyID);

    var model = BuildPlaceModel(services, caller.Id);
    model.BoxLabel = box.BoxLabel;

    await model.OnGetAsync();

    Assert.NotNull(model.FoundBox);
    Assert.Equal(box.BoxID, model.FoundBox!.BoxID);
}

[Fact]
public async Task Place_OnPostAsync_CallerIsEditorOnDifferentStudyThanBox_ReturnsForbid_BoxNotMoved()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg21@test.com");

    var ownStudy = new Study { StudyCode = "RWG-P-OWN" };
    var otherStudy = new Study { StudyCode = "RWG-P-OTHER" };
    context.Studies.AddRange(ownStudy, otherStudy);
    await context.SaveChangesAsync();

    var caller = await CreateUserAsync(services, "editor-rwg21@test.com");
    await GrantAsync(context, caller.Id, ownStudy.StudyID, "Editor", sysAdmin.Id);

    // Box belongs to a study the caller has no access to.
    var box = await AddBoxAsync(context, "RWG-P-BOX3", otherStudy.StudyID);

    var model = BuildPlaceModel(services, caller.Id);
    model.BoxId = box.BoxID;
    model.NewRackId = 999;

    var result = await model.OnPostAsync();

    Assert.IsType<ForbidResult>(result);

    var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
    Assert.Null(reloaded.RackID);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `STASIS_TEST_CONNECTION="Host=localhost;Port=5432;Database=stasis_test;Username=stasis_app;Password=<pwd>" dotnet test --filter "FullyQualifiedName~RemainingWriteGatesTests"`
Expected: the two new `OnGetAsync` tests FAIL (`Place_OnGetAsync_CallerWithNoAccessToBoxStudy_DoesNotReturnBox` because `OnGetAsync` currently returns an unrestricted `Task`, not usable for assertion the way it's called — adjust the test to call `await model.OnGetAsync()` once `OnGetAsync` is `async Task` as planned in Step 3; until then this won't compile against the *current* `void`-returning signature's leak). The `OnPostAsync` cross-study test FAILS with `RedirectToPageResult` instead of `ForbidResult` (box gets moved).

- [ ] **Step 3: Fix `OnGetAsync` in `Pages/Boxes/Place.cshtml.cs`** to resolve the caller's
  `visibleStudyIds` the same way every other box-lookup page does, and pass it into
  `GetBoxByLabelAsync` instead of `null`:

```csharp
public async Task OnGetAsync()
{
    if (!string.IsNullOrEmpty(BoxLabel))
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);
        var visibleStudyIds = isSystemAdmin ? null : await _studyAccessService.GetVisibleStudyIdsAsync(userId);

        FoundBox = await _storageService.GetBoxByLabelAsync(BoxLabel, visibleStudyIds);
    }
    await LoadOptionsAsync();
}
```

Remove the now-stale "study-scoped filtering ... is out of scope" comment above the old call.

- [ ] **Step 4: Fix `OnPostAsync`** to re-validate the specific box's study before moving it,
  mirroring `Pages/Boxes/Move.cshtml.cs`'s `OnPostMoveAsync` pattern (fetch, check
  `CanEditAsync` against the resolved `StudyID`, only proceed if it passes or the box is
  unclaimed):

```csharp
public async Task<IActionResult> OnPostAsync()
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    if (!await IsEditorAnywhereAsync(userId))
    {
        return Forbid();
    }

    var box = await _storageService.GetBoxWithSpecimens(BoxId, null);
    if (box == null)
    {
        return NotFound();
    }

    if (box.StudyID.HasValue && !await _studyAccessService.CanEditAsync(userId, box.StudyID.Value))
    {
        return Forbid();
    }

    await _storageService.MoveBoxAsync(BoxId, NewRackId, userId);
    TempData["Success"] = "Box moved successfully.";
    return RedirectToPage();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: same filter as Step 2.
Expected: PASS, including the two pre-existing `Place_OnPostAsync_*` tests (no-editor-anywhere
Forbid, editor-moves-unclaimed-box succeeds) — confirm those still pass unmodified.

- [ ] **Step 6: Run the full test suite**

Run: `STASIS_TEST_CONNECTION=... dotnet test`
Expected: all tests pass (105 existing + 3 new = 108).

- [ ] **Step 7: Commit**

```bash
git add Pages/Boxes/Place.cshtml.cs STASIS.Tests/Integration/RemainingWriteGatesTests.cs
git commit -m "Restored per-study authorization to Boxes/Place OnGetAsync and OnPostAsync"
```

---

## Task 2: MoveToTempAsync — claim the Temp box for the specimen's study

**Files:**
- Modify: `STASIS/Services/StorageService.cs:216-238` (`MoveToTempAsync`)
- Test: `STASIS.Tests/Services/BoxStudyEnforcementTests.cs`

**Interfaces:**
- Consumes: `ValidateAndAssignBoxStudyAsync(int boxId, int studyId)` (existing, modified by
  Task 6 — this task's call must still work against Task 6's updated signature/body since both
  land on this method; if Task 6 lands first, this task's new call automatically gets the
  stricter ambiguous-box check for free).

- [ ] **Step 1: Write the failing test** in `BoxStudyEnforcementTests.cs` (reuse its
  `CreateService`/`AddStudyAsync`/`AddBoxAsync`/`AddSpecimenAsync` helpers):

```csharp
[Fact]
public async Task MoveToTempAsync_MovesSpecimenIntoTempBox_AndClaimsTempBoxForItsStudy()
{
    var context = TestDbContextFactory.Create();
    var service = CreateService(context);

    var studyA = await AddStudyAsync(context, "MTT-STUDY-A");
    var sourceBox = await AddBoxAsync(context, "MTT-SOURCE", studyA.StudyID, "Standard");
    var tempBox = await AddBoxAsync(context, "MTT-TEMP", null, "Temp");
    var specimen = await AddSpecimenAsync(context, "MTT-S1", studyA.StudyID, sourceBox.BoxID, 0, 0);

    await service.MoveToTempAsync(specimen.SpecimenID, "test-user");

    var reloadedTemp = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == tempBox.BoxID);
    var reloadedSpecimen = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen.SpecimenID);

    Assert.Equal(studyA.StudyID, reloadedTemp.StudyID);
    Assert.Equal(tempBox.BoxID, reloadedSpecimen.BoxID);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~BoxStudyEnforcementTests.MoveToTempAsync_MovesSpecimenIntoTempBox"`
Expected: FAIL — `reloadedTemp.StudyID` is `null`.

- [ ] **Step 3: Fix `MoveToTempAsync`** in `StorageService.cs` to call
  `ValidateAndAssignBoxStudyAsync` before reassigning the specimen, mirroring
  `MoveSpecimenAsync`'s ordering (validate/assign the destination box's study before mutating the
  specimen):

```csharp
public async Task MoveToTempAsync(int specimenId, string userId)
{
    var specimen = await _context.Specimens.FindAsync(specimenId);
    if (specimen == null) return;

    var tempBox = await _context.Boxes
        .FirstOrDefaultAsync(b => b.BoxCategory == "Temp");
    if (tempBox == null) return;

    await ValidateAndAssignBoxStudyAsync(tempBox.BoxID, specimen.StudyID);

    var oldBoxId = specimen.BoxID;
    specimen.BoxID = tempBox.BoxID;
    specimen.PositionRow = null;
    specimen.PositionCol = null;
    specimen.Status = "Temp";

    await _context.SaveChangesAsync();

    await _auditService.LogChangeAsync("tbl_Specimens", specimen.SpecimenID.ToString(),
        "MovedToTemp", $"Box {oldBoxId}", $"Temp ({tempBox.BoxLabel})", userId);

    if (oldBoxId.HasValue)
        await CheckAndUnassignEmptyBoxAsync(oldBoxId.Value);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: same filter as Step 2.
Expected: PASS.

- [ ] **Step 5: Run the full `BoxStudyEnforcementTests` class** to confirm no regression

Run: `dotnet test --filter "FullyQualifiedName~BoxStudyEnforcementTests"`
Expected: all PASS (existing `CheckAndUnassignEmptyBoxAsync` tests unaffected, since this change
only touches `MoveToTempAsync`'s own body).

- [ ] **Step 6: Commit**

```bash
git add STASIS/Services/StorageService.cs STASIS.Tests/Services/BoxStudyEnforcementTests.cs
git commit -m "Fixed MoveToTempAsync leaving the shared Temp box invisible after use"
```

---

## Task 3: Samples/Add — stop leaking box inventory on a rejected request

**Files:**
- Modify: `Pages/Samples/Add.cshtml.cs` (`OnPostAsync`, ~line 98-116)
- Test: `STASIS.Tests/Integration/AddSampleAccessTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.CanEditAsync(string, int)`, `IStudyAccessService.IsSystemAdminAsync(string)`
  (existing); `LoadDropdownsAsync(string userId, bool isSystemAdmin, int? selectedStudyId)`
  (existing private method, unchanged signature — this task changes what's *passed in*, not the
  method itself).

- [ ] **Step 1: Write the failing test** in `AddSampleAccessTests.cs` (reuse its
  `BuildAddModel`/study/user helpers — follow the file's existing patterns for creating a Study,
  an Editor on a different study, and binding `model.StudyID`/`model.Samples` before calling
  `OnPostAsync`):

```csharp
[Fact]
public async Task OnPostAsync_PostedStudyCallerCannotEdit_DoesNotPopulateBoxesForThatStudy()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-add-leak1@test.com");

    var ownStudy = new Study { StudyCode = "ADD-LEAK-OWN" };
    var otherStudy = new Study { StudyCode = "ADD-LEAK-OTHER" };
    context.Studies.AddRange(ownStudy, otherStudy);
    await context.SaveChangesAsync();

    var caller = await CreateUserAsync(services, "editor-add-leak1@test.com");
    await GrantAsync(context, caller.Id, ownStudy.StudyID, "Editor", sysAdmin.Id);

    var freezer = new Freezer { FreezerName = "ADD-LEAK-FREEZER" };
    context.Freezers.Add(freezer);
    await context.SaveChangesAsync();
    var compartment = new Compartment { CompartmentName = "ADD-LEAK-COMPARTMENT", FreezerID = freezer.FreezerID };
    context.Compartments.Add(compartment);
    await context.SaveChangesAsync();
    var rack = new Rack { RackName = "ADD-LEAK-RACK", CompartmentID = compartment.CompartmentID };
    context.Racks.Add(rack);
    await context.SaveChangesAsync();
    var otherStudyBox = new Box { BoxLabel = "ADD-LEAK-SECRETBOX", BoxType = "81-slot", BoxCategory = "Standard", StudyID = otherStudy.StudyID, RackID = rack.RackID };
    context.Boxes.Add(otherStudyBox);
    await context.SaveChangesAsync();

    var model = BuildAddModel(services, caller.Id);
    model.StudyID = otherStudy.StudyID; // caller cannot edit this study
    model.Samples = new List<AddModel.SampleInput> { new() { BarcodeID = "ADD-LEAK-001" } };

    var result = await model.OnPostAsync();

    Assert.False(model.AllBoxes.Any(b => b.BoxID == otherStudyBox.BoxID));
}
```

(`AddModel.SampleInput` is the existing bind-property element type at `Pages/Samples/Add.cshtml.cs:50`,
with a `string? BarcodeID` property. `model.AllBoxes`'s element type is `AddModel.BoxOption` with
a `BoxID` property — confirm both against the current file if they've shifted.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AddSampleAccessTests.OnPostAsync_PostedStudyCallerCannotEdit"`
Expected: FAIL — `model.AllBoxes` contains `otherStudyBox`.

- [ ] **Step 3: Fix `OnPostAsync`** in `Pages/Samples/Add.cshtml.cs` to resolve editability
  before loading dropdowns, and only pass the posted `StudyID` through to `LoadDropdownsAsync`
  when the caller can actually edit it:

```csharp
public async Task<IActionResult> OnPostAsync()
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);

    // Re-validate the posted StudyID server-side before using it for anything, including
    // rendering dropdowns -- the Study dropdown only offers editable studies, but a caller can
    // post any StudyID directly, so don't trust it enough to even reveal that study's box
    // inventory on a later validation failure.
    var studyIdIsEditable = StudyID.HasValue &&
        (isSystemAdmin || await _studyAccessService.CanEditAsync(userId, StudyID.Value));

    await LoadDropdownsAsync(userId, isSystemAdmin, studyIdIsEditable ? StudyID : null);

    if (!ModelState.IsValid)
    {
        return Page();
    }

    if (!studyIdIsEditable)
    {
        ModelState.AddModelError(string.Empty, "You do not have permission to add samples to this study.");
        return Page();
    }

    // ... rest of the method (activeSamples filtering, save) is unchanged from here.
```

Delete the old standalone `CanEditAsync` check that previously sat after the `ModelState.IsValid`
check (now folded into `studyIdIsEditable` above).

- [ ] **Step 4: Run the test to verify it passes**

Run: same filter as Step 2.
Expected: PASS.

- [ ] **Step 5: Run the full `AddSampleAccessTests` class** to confirm the authorized-caller path
  still works (Review Focus item: a rejected POST for an editable study must still redisplay that
  study's boxes)

Run: `dotnet test --filter "FullyQualifiedName~AddSampleAccessTests"`
Expected: all PASS, including existing tests that post a valid/editable `StudyID` with an invalid
barcode and expect the box dropdown to still be populated.

- [ ] **Step 6: Commit**

```bash
git add Pages/Samples/Add.cshtml.cs STASIS.Tests/Integration/AddSampleAccessTests.cs
git commit -m "Stopped Samples/Add from leaking box inventory for a study the caller cannot edit"
```

---

## Task 4: Discard — close the existence leak on the confirm step

**Files:**
- Modify: `Pages/Samples/Discard.cshtml.cs` (`OnPostRequestAsync`, ~line 88-143)
- Test: new test in `STASIS.Tests/Integration/RemainingWriteGatesTests.cs` (this file already has
  a `Samples/Discard` section with `Discard_OnPostRequestAsync_MixOfEditableAndNonEditableStudySpecimens_DiscardsOnlyEditableOnes`
  — add alongside it)

- [ ] **Step 1: Write the failing test**, reusing this file's existing
  `CreateSystemAdminAsync`/`CreateUserAsync`/`GrantAsync`/`AddBoxAsync`/`AddSpecimenAsync`
  helpers and `BuildDiscardModel`:

```csharp
[Fact]
public async Task Discard_OnPostRequestAsync_SpecimenInStudyCallerCannotView_NotRevealedInMessage()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg-disc2@test.com");

    var hiddenStudy = new Study { StudyCode = "RWG-DISC-HIDDEN" };
    context.Studies.Add(hiddenStudy);
    await context.SaveChangesAsync();

    // Caller has zero access to hiddenStudy -- cannot even view it.
    var caller = await CreateUserAsync(services, "noaccess-rwg-disc2@test.com");

    await AddSpecimenAsync(context, "RWG-DISC-HIDDEN-001", hiddenStudy.StudyID, null, null, null);

    var model = BuildDiscardModel(services, caller.Id);
    model.Barcodes = "RWG-DISC-HIDDEN-001";

    var result = await model.OnPostRequestAsync();

    Assert.Null(model.CreatedApproval);
    Assert.False(model.TempData.ContainsKey("Success"));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~RemainingWriteGatesTests.Discard_OnPostRequestAsync_SpecimenInStudyCallerCannotView"`
Expected: FAIL — `TempData["Success"]` is set, containing the hidden study's `StudyCode`.

- [ ] **Step 3: Fix `OnPostRequestAsync`** in `Pages/Samples/Discard.cshtml.cs` to apply the same
  `CanViewAsync` existence-hiding gate `OnPostLookupAsync` already applies, before the status
  checks:

```csharp
foreach (var barcode in barcodeList)
{
    var matches = await _sampleService.GetSpecimensByBarcode(barcode);
    foreach (var specimen in matches)
    {
        // A specimen in a study the caller cannot even view must not be revealed to exist,
        // mirroring OnPostLookupAsync's guard.
        if (!await _studyAccessService.CanViewAsync(userId, specimen.StudyID))
        {
            continue;
        }

        if (specimen.Status != "Discarded" &&
            specimen.Status != "Shipped" &&
            specimen.DiscardApprovalID == null)
        {
            if (await _studyAccessService.CanEditAsync(userId, specimen.StudyID))
            {
                specimenIds.Add(specimen.SpecimenID);
            }
            else
            {
                skippedBarcodes.Add($"{barcode} ({specimen.Study?.StudyCode}) — no edit access to this study");
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: same filter as Step 2.
Expected: PASS.

- [ ] **Step 5: Run the full `RemainingWriteGatesTests` class** to confirm the existing
  mixed-batch Discard test still passes unmodified (Review Focus: viewable-but-not-editable
  specimens must still be correctly skipped-with-message, not silently dropped)

Run: `dotnet test --filter "FullyQualifiedName~RemainingWriteGatesTests"`
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add Pages/Samples/Discard.cshtml.cs STASIS.Tests/Integration/RemainingWriteGatesTests.cs
git commit -m "Closed Discard's specimen-existence leak on the confirm step"
```

---

## Task 5: Move the "last Study Admin" guardrail into StudyAccessService

**Files:**
- Modify: `STASIS/Services/StudyAccessService.cs` (`RevokeAccessAsync`, `ChangeRoleAsync`)
- Modify: `Pages/Administration/EditUser.cshtml.cs` (`OnPostRevokeAccessAsync`,
  `OnPostChangeRoleAsync`) — remove the now-duplicated inline checks
- Test: `STASIS.Tests/Services/StudyAccessServiceTests.cs`,
  `STASIS.Tests/Integration/EditUserAccessTests.cs` (existing guardrail tests there must keep
  passing unmodified — this task moves the check, it does not change observable behavior)

**Interfaces:**
- Produces: `RevokeAccessAsync`/`ChangeRoleAsync` now throw
  `InvalidOperationException` when the operation would drop a study below one Study Admin,
  instead of relying on the caller to have already checked `CountStudyAdminsAsync`. Page
  callers catch this and convert it to their existing `TempData["Error"]` + redirect behavior.

- [ ] **Step 1: Write the failing tests** in `StudyAccessServiceTests.cs` (reuse its existing
  `TestDbContextFactory`-based setup pattern):

```csharp
[Fact]
public async Task RevokeAccessAsync_LastStudyAdmin_Throws_AndRowUnchanged()
{
    var context = TestDbContextFactory.Create();
    var (userManager, _) = IdentityTestFactory.Create(context);
    var service = new StudyAccessService(context, userManager, new AuditService(context));

    var study = new Study { StudyCode = "SAS-LASTADMIN" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    var user = new IdentityUser { UserName = "lastadmin@test.com", Email = "lastadmin@test.com" };
    await userManager.CreateAsync(user);

    context.UserStudyAccesses.Add(new UserStudyAccess
    {
        AspNetUserId = user.Id, StudyID = study.StudyID, Role = "StudyAdmin",
        GrantedByUserId = "granter", GrantedDate = DateTime.UtcNow
    });
    await context.SaveChangesAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.RevokeAccessAsync(user.Id, study.StudyID, "granter"));

    var stillThere = await context.UserStudyAccesses.AsNoTracking()
        .SingleOrDefaultAsync(a => a.AspNetUserId == user.Id && a.StudyID == study.StudyID);
    Assert.NotNull(stillThere);
}

[Fact]
public async Task ChangeRoleAsync_DemotingLastStudyAdmin_Throws_AndRoleUnchanged()
{
    var context = TestDbContextFactory.Create();
    var (userManager, _) = IdentityTestFactory.Create(context);
    var service = new StudyAccessService(context, userManager, new AuditService(context));

    var study = new Study { StudyCode = "SAS-LASTADMIN-DEMOTE" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    var user = new IdentityUser { UserName = "lastadmin2@test.com", Email = "lastadmin2@test.com" };
    await userManager.CreateAsync(user);

    context.UserStudyAccesses.Add(new UserStudyAccess
    {
        AspNetUserId = user.Id, StudyID = study.StudyID, Role = "StudyAdmin",
        GrantedByUserId = "granter", GrantedDate = DateTime.UtcNow
    });
    await context.SaveChangesAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.ChangeRoleAsync(user.Id, study.StudyID, "Editor", "granter"));

    var reloaded = await context.UserStudyAccesses.AsNoTracking()
        .SingleAsync(a => a.AspNetUserId == user.Id && a.StudyID == study.StudyID);
    Assert.Equal("StudyAdmin", reloaded.Role);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StudyAccessServiceTests.RevokeAccessAsync_LastStudyAdmin"`
Expected: FAIL — no exception thrown, row deleted/changed.

- [ ] **Step 3: Add the guardrail to `RevokeAccessAsync` and `ChangeRoleAsync`** in
  `StudyAccessService.cs`, using the existing `CountStudyAdminsAsync`:

```csharp
public async Task RevokeAccessAsync(string userId, int studyId, string revokedByUserId)
{
    var access = await _context.UserStudyAccesses
        .FirstOrDefaultAsync(a => a.AspNetUserId == userId && a.StudyID == studyId);

    if (access == null)
    {
        return;
    }

    if (access.Role == "StudyAdmin" && await CountStudyAdminsAsync(studyId) <= 1)
    {
        throw new InvalidOperationException("A study must have at least one Study Admin.");
    }

    var oldRole = access.Role;
    var recordId = access.UserStudyAccessID;

    _context.UserStudyAccesses.Remove(access);
    await _context.SaveChangesAsync();

    await _auditService.LogChangeAsync("tbl_UserStudyAccess", recordId.ToString(),
        "Revoked", oldRole, null, revokedByUserId);
}

public async Task ChangeRoleAsync(string userId, int studyId, string newRole, string changedByUserId)
{
    var access = await _context.UserStudyAccesses
        .FirstOrDefaultAsync(a => a.AspNetUserId == userId && a.StudyID == studyId);

    if (access == null)
    {
        return;
    }

    if (access.Role == "StudyAdmin" && newRole != "StudyAdmin" && await CountStudyAdminsAsync(studyId) <= 1)
    {
        throw new InvalidOperationException("A study must have at least one Study Admin.");
    }

    var oldRole = access.Role;
    access.Role = newRole;
    await _context.SaveChangesAsync();

    await _auditService.LogChangeAsync("tbl_UserStudyAccess", access.UserStudyAccessID.ToString(),
        "RoleChanged", oldRole, newRole, changedByUserId);
}
```

Update the doc comments above both methods in `IStudyAccessService.cs` to say the guardrail is
now enforced by the method itself, not delegated to callers.

- [ ] **Step 4: Run the tests to verify they pass**

Run: same filter as Step 2 plus the `ChangeRoleAsync` test.
Expected: PASS.

- [ ] **Step 5: Simplify `EditUser.cshtml.cs`'s two handlers** to catch the exception instead of
  pre-checking `CountStudyAdminsAsync` themselves:

```csharp
public async Task<IActionResult> OnPostRevokeAccessAsync(string userId, int studyId)
{
    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    if (!await _studyAccessService.CanManageStudyAsync(currentUserId, studyId))
    {
        TempData["Error"] = "You do not have permission to manage that study.";
        return RedirectToPage(new { id = userId });
    }

    var currentRole = await _studyAccessService.GetRoleAsync(userId, studyId);
    if (currentRole == StudyRole.None)
    {
        TempData["Error"] = "That user has no access to this study.";
        return RedirectToPage(new { id = userId });
    }

    try
    {
        await _studyAccessService.RevokeAccessAsync(userId, studyId, currentUserId);
    }
    catch (InvalidOperationException ex)
    {
        TempData["Error"] = ex.Message;
        return RedirectToPage(new { id = userId });
    }

    TempData["Success"] = "Access revoked.";
    return RedirectToPage(new { id = userId });
}
```

Apply the same `try`/`catch` shape to `OnPostChangeRoleAsync`, removing its inline
`CountStudyAdminsAsync` block.

- [ ] **Step 6: Run `EditUserAccessTests.cs`** to confirm the existing page-level guardrail tests
  (added when this feature first shipped) still pass through the new exception-based path
  unmodified

Run: `dotnet test --filter "FullyQualifiedName~EditUserAccessTests"`
Expected: all PASS — same `TempData["Error"]` message, same "row unchanged" assertions.

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: all PASS.

- [ ] **Step 8: Commit**

```bash
git add STASIS/Services/StudyAccessService.cs STASIS/Services/IStudyAccessService.cs \
  Pages/Administration/EditUser.cshtml.cs STASIS.Tests/Services/StudyAccessServiceTests.cs
git commit -m "Moved the last-Study-Admin guardrail into StudyAccessService itself"
```

---

## Task 6: Ambiguous legacy boxes — deny writes consistently with the read-side hide rule

**Files:**
- Modify: `STASIS/Services/StorageService.cs` (`ValidateAndAssignBoxStudyAsync`, ~line 276)
- Modify: `Pages/Samples/Add.cshtml.cs` (`CanAccessBoxForAddAsync`, ~line 343)
- Test: `STASIS.Tests/Services/BoxStudyEnforcementTests.cs`,
  `STASIS.Tests/Integration/AddSampleAccessTests.cs`

**Interfaces:**
- No signature changes to either method.

- [ ] **Step 1: Write the failing test** in `BoxStudyEnforcementTests.cs`:

```csharp
[Fact]
public async Task ValidateAndAssignBoxStudyAsync_OnNullStudyIdBoxWithSpecimensFromDifferentStudy_Throws()
{
    var context = TestDbContextFactory.Create();
    var service = CreateService(context);

    var studyA = await AddStudyAsync(context, "AMBIG-STUDY-A");
    var studyB = await AddStudyAsync(context, "AMBIG-STUDY-B");

    // Box left with StudyID == null because its specimens already span two studies -- the
    // exact shape BackfillBoxStudyId leaves for manual cleanup.
    var box = await AddBoxAsync(context, "AMBIG-BOX", null, "Standard");
    await AddSpecimenAsync(context, "AMBIG-S1", studyA.StudyID, box.BoxID, 0, 0);
    await AddSpecimenAsync(context, "AMBIG-S2", studyB.StudyID, box.BoxID, 0, 1);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.ValidateAndAssignBoxStudyAsync(box.BoxID, studyA.StudyID));

    var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
    Assert.Null(reloaded.StudyID);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~BoxStudyEnforcementTests.ValidateAndAssignBoxStudyAsync_OnNullStudyIdBoxWithSpecimensFromDifferentStudy"`
Expected: FAIL — no exception, `box.StudyID` silently set to `studyA.StudyID`.

- [ ] **Step 3: Fix `ValidateAndAssignBoxStudyAsync`** in `StorageService.cs` to load the box's
  specimens and reject the ambiguous-box case:

```csharp
public async Task ValidateAndAssignBoxStudyAsync(int boxId, int studyId)
{
    var box = await _context.Boxes
        .Include(b => b.Specimens)
        .FirstOrDefaultAsync(b => b.BoxID == boxId);
    if (box == null)
        return;

    if (box.StudyID.HasValue && box.StudyID.Value != studyId)
    {
        throw new InvalidOperationException(
            $"Box {boxId} is already assigned to study {box.StudyID.Value} and cannot also hold specimens from study {studyId}.");
    }

    if (!box.StudyID.HasValue)
    {
        // A null StudyID with existing specimens from a different study is the ambiguous
        // legacy-box case the BackfillBoxStudyId migration leaves for manual admin cleanup --
        // must not be silently claimed for whichever study happens to come along next.
        if (box.Specimens.Any(s => s.StudyID != studyId))
        {
            throw new InvalidOperationException(
                $"Box {boxId} already holds specimens from a different study than {studyId} and has no assigned StudyID; it needs manual cleanup before more specimens can be added.");
        }

        box.StudyID = studyId;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes, then run the full class**

Run: `dotnet test --filter "FullyQualifiedName~BoxStudyEnforcementTests"`
Expected: all PASS, including the pre-existing
`ValidateAndAssignBoxStudyAsync_OnEmptyUnassignedBox_SetsStudyID` test (zero specimens — the
`Any()` check is vacuously false, behavior unchanged for that case, per this plan's Review
Focus item on genuinely-unclaimed boxes).

- [ ] **Step 5: Write the failing test** for `CanAccessBoxForAddAsync` in
  `AddSampleAccessTests.cs`:

```csharp
[Fact]
public async Task CanAccessBoxForAddAsync_AmbiguousNullStudyIdBox_DeniedToNonSystemAdmin()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ambig1@test.com");

    var studyA = new Study { StudyCode = "ADD-AMBIG-A" };
    var studyB = new Study { StudyCode = "ADD-AMBIG-B" };
    context.Studies.AddRange(studyA, studyB);
    await context.SaveChangesAsync();

    var caller = await CreateUserAsync(services, "editor-ambig1@test.com");
    await GrantAsync(context, caller.Id, studyA.StudyID, "Editor", sysAdmin.Id);

    var box = new Box { BoxLabel = "ADD-AMBIG-BOX", BoxType = "81-slot", BoxCategory = "Standard", StudyID = null };
    context.Boxes.Add(box);
    await context.SaveChangesAsync();
    await AddSpecimenAsync(context, "ADD-AMBIG-S1", studyA.StudyID, box.BoxID, 0, 0);
    await AddSpecimenAsync(context, "ADD-AMBIG-S2", studyB.StudyID, box.BoxID, 0, 1);

    var model = BuildAddModel(services, caller.Id);

    var result = await model.OnGetNextPositionAsync(box.BoxID);

    Assert.IsType<ForbidResult>(result);
}
```

(`OnGetNextPositionAsync(int boxId, string? claimed = null)` is confirmed at
`Pages/Samples/Add.cshtml.cs:293` — this task doesn't change its signature, only the
`CanAccessBoxForAddAsync` gate it calls internally.)

- [ ] **Step 6: Run the test to verify it fails**

Expected: FAIL — `IsEditorAnywhereAsync` returns true for the caller (Editor on Study A anywhere),
so the ambiguous box is accessible.

- [ ] **Step 7: Fix `CanAccessBoxForAddAsync`** in `Pages/Samples/Add.cshtml.cs`:

```csharp
private async Task<bool> CanAccessBoxForAddAsync(string userId, int boxId)
{
    var box = await _storageService.GetBoxWithSpecimens(boxId, null);
    if (box == null)
        return false;

    if (box.StudyID is int studyId)
        return await _studyAccessService.CanEditAsync(userId, studyId);

    // StudyID is null: either genuinely unclaimed (zero specimens -- claimable by any editor
    // anywhere) or an ambiguous legacy box whose specimens already span more than one study,
    // left for manual admin cleanup -- must not be treated as claimable, mirroring
    // StorageService.ApplyStudyVisibility's rule hiding such boxes from non-admins.
    if (box.Specimens.Count > 0)
        return await _studyAccessService.IsSystemAdminAsync(userId);

    return await IsEditorAnywhereAsync(userId);
}
```

- [ ] **Step 8: Run the test to verify it passes, then run the full `AddSampleAccessTests` class**

Run: `dotnet test --filter "FullyQualifiedName~AddSampleAccessTests"`
Expected: all PASS.

- [ ] **Step 9: Run the full test suite**

Run: `dotnet test`
Expected: all PASS.

- [ ] **Step 10: Commit**

```bash
git add STASIS/Services/StorageService.cs Pages/Samples/Add.cshtml.cs \
  STASIS.Tests/Services/BoxStudyEnforcementTests.cs STASIS.Tests/Integration/AddSampleAccessTests.cs
git commit -m "Denied writes to ambiguous null-StudyID boxes holding multiple studies' specimens"
```

---

## Task 7: Guard the barcode/study migration against legacy null StudyID rows

**Files:**
- Modify: `Migrations/20260509184420_BarcodeStudyCompositeKey.cs` (`Up`)
- Test: new file `STASIS.Tests/Migrations/BarcodeStudyCompositeKeyMigrationTests.cs` (mirror
  `BackfillBoxStudyIdMigrationTests.cs`'s structure: `IClassFixture<TestWebApplicationFactory>`,
  `IAsyncLifetime`, requires `STASIS_TEST_CONNECTION`, runs raw SQL copied verbatim from the
  migration against a real Postgres database)

- [ ] **Step 1: Write the failing test**:

```csharp
using Npgsql;
// ... other usings matching BackfillBoxStudyIdMigrationTests.cs

public class BarcodeStudyCompositeKeyMigrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public BarcodeStudyCompositeKeyMigrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Copied verbatim from the guard this task adds to Migrations/20260509184420_BarcodeStudyCompositeKey.cs's Up() method.
    private const string NullStudyIdGuardSql = @"
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM ""tbl_Specimens"" WHERE ""StudyID"" IS NULL) THEN
                RAISE EXCEPTION 'tbl_Specimens has rows with StudyID IS NULL; assign a StudyID to every specimen before applying this migration.';
            END IF;
        END $$;";

    [Fact]
    public async Task NullStudyIdGuard_RowWithNullStudyId_Throws()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StasisDbContext>();

        // Bypass the model's current non-nullable StudyID to simulate a pre-migration legacy row.
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"tbl_Specimens\" ALTER COLUMN \"StudyID\" DROP NOT NULL;");
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"tbl_Specimens\" (\"BarcodeID\", \"Status\") VALUES ('GUARD-TEST', 'In-Stock');");

            await Assert.ThrowsAsync<PostgresException>(
                () => context.Database.ExecuteSqlRawAsync(NullStudyIdGuardSql));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"tbl_Specimens\" WHERE \"BarcodeID\" = 'GUARD-TEST';");
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"tbl_Specimens\" ALTER COLUMN \"StudyID\" SET NOT NULL;");
        }
    }

    [Fact]
    public async Task NullStudyIdGuard_NoNullRows_DoesNotThrow()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StasisDbContext>();

        await context.Database.ExecuteSqlRawAsync(NullStudyIdGuardSql);
        // No assertion needed beyond "did not throw".
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `STASIS_TEST_CONNECTION=... dotnet test --filter "FullyQualifiedName~BarcodeStudyCompositeKeyMigrationTests"`
Expected: `NullStudyIdGuard_RowWithNullStudyId_Throws` FAILS (`NullStudyIdGuardSql` isn't referenced
by production code yet, but the test itself should already pass once written, since it exercises
only the constant — if it fails, it's because the `DO $$ ... END $$;` block syntax needs
adjusting for Npgsql's raw-SQL execution; fix the SQL, not the test's intent).
`NullStudyIdGuard_NoNullRows_DoesNotThrow` should already PASS since `ResetDatabaseAsync` leaves
no null rows.

- [ ] **Step 3: Add the guard to the migration's `Up()` method** in
  `Migrations/20260509184420_BarcodeStudyCompositeKey.cs`, as the first statement, before the
  existing `DropForeignKey` call:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM ""tbl_Specimens"" WHERE ""StudyID"" IS NULL) THEN
                RAISE EXCEPTION 'tbl_Specimens has rows with StudyID IS NULL; assign a StudyID to every specimen before applying this migration.';
            END IF;
        END $$;");

    migrationBuilder.DropForeignKey(
        name: "FK_tbl_Specimens_tbl_Studies_StudyID",
        table: "tbl_Specimens");

    // ... rest of Up() unchanged
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: same filter as Step 2.
Expected: both PASS.

- [ ] **Step 5: Run the full test suite** to confirm this doesn't break `dotnet ef database
  update` on the dev/test databases (both already have this migration applied with zero null
  rows, so re-running the test suite's migration-apply path exercises the new guard harmlessly)

Run: `STASIS_TEST_CONNECTION=... dotnet test`
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add Migrations/20260509184420_BarcodeStudyCompositeKey.cs \
  STASIS.Tests/Migrations/BarcodeStudyCompositeKeyMigrationTests.cs
git commit -m "Added a loud guard against legacy null-StudyID specimens in the barcode migration"
```

---

## Task 8: Shipments/Create — restore study-scoped access, with per-row enforcement

**Scope note:** this task is materially bigger than the other low/medium findings. The naive fix
(swap `[Authorize(Roles = "Admin")]` for an "editor anywhere" page gate) is *wrong*: this page's
CSV import resolves each row to an *existing* specimen by barcode, and that specimen's `StudyID`
is not known until the service looks it up mid-loop — exactly the shape `Pages/Samples/Import.cshtml.cs`
already solves for new-specimen creation and `Pages/Samples/Discard.cshtml.cs` already solves for
existing-specimen lookup. A page-level-only gate would let an Editor of Study A include Study B's
barcodes in their shipment CSV with zero per-row check — a real IDOR, not a regression. The
correct fix pushes an `editableStudyIds` set into `IShipmentService.ImportBatchFromCsvAsync` and
skips non-editable rows there, mirroring `ISampleService.ImportSpecimensFromCsv`'s existing
`editableStudyIds` parameter.

**Files:**
- Modify: `STASIS/Services/IShipmentService.cs`, `STASIS/Services/ShipmentService.cs`
  (`ImportBatchFromCsvAsync`, ~line 19-143)
- Modify: `Pages/Shipments/Create.cshtml.cs`
- Modify: `STASIS/Models/ShipmentBatch.cs` (add one counter field, mirrors
  `TotalNotFound`/`TotalPreviouslyShipped`/etc. already on this model)
- Test: new file `STASIS.Tests/Integration/ShipmentsCreateAccessTests.cs` (this page currently
  has no dedicated integration test file — follow `AddSampleAccessTests.cs`'s structure:
  `IClassFixture<TestWebApplicationFactory>`, `IAsyncLifetime`, `BuildCreateModel` helper mirroring
  `RemainingWriteGatesTests.cs`'s `BuildCreateModel`/`BuildPlaceModel` pattern but for
  `STASIS.Pages.Shipments.CreateModel`)

**Interfaces:**
- Modify: `Task<ShipmentBatch> ImportBatchFromCsvAsync(Stream csvStream, string userId, string? requestorName, string? requestorEmail, IReadOnlyCollection<int>? editableStudyIds)`
  — new fifth parameter, `null` meaning unrestricted (same null-means-unrestricted convention as
  every other method in this codebase that takes `visibleStudyIds`/`editableStudyIds`).
- Produces: `ShipmentBatch.TotalNoAccess` (new `int` property) — count of rows matched to a real
  specimen the caller cannot edit, surfaced the same way `TotalNotFound` etc. already are.

- [ ] **Step 1: Write the failing service-level test** in a new file
  `STASIS.Tests/Services/ShipmentServiceStudyEnforcementTests.cs`, following
  `BoxStudyEnforcementTests.cs`'s in-memory-`StasisDbContext`-via-`TestDbContextFactory` pattern
  (no `STASIS_TEST_CONNECTION` needed — this doesn't touch raw SQL):

```csharp
[Fact]
public async Task ImportBatchFromCsvAsync_BarcodeInNonEditableStudy_SkippedAsNoAccess_NotMatched()
{
    var context = TestDbContextFactory.Create();
    var service = new ShipmentService(context, new AuditService(context));

    var editableStudy = new Study { StudyCode = "SHIP-EDITABLE" };
    var otherStudy = new Study { StudyCode = "SHIP-OTHER" };
    context.Studies.AddRange(editableStudy, otherStudy);
    await context.SaveChangesAsync();

    context.Specimens.Add(new Specimen { BarcodeID = "SHIP-S1", StudyID = otherStudy.StudyID, Status = "In-Stock" });
    await context.SaveChangesAsync();

    var csv = "Barcode\nSHIP-S1";
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

    var batch = await service.ImportBatchFromCsvAsync(stream, "test-user", null, null,
        new[] { editableStudy.StudyID });

    Assert.Equal(1, batch.TotalNoAccess);
    Assert.Equal(0, batch.TotalAvailable);

    var request = await context.ShipmentRequests.AsNoTracking()
        .SingleAsync(r => r.BatchID == batch.BatchID && r.RequestedBarcode == "SHIP-S1");
    Assert.Equal("No Access", request.Status);
    Assert.Null(request.MatchedSpecimenID);
}

[Fact]
public async Task ImportBatchFromCsvAsync_EditableStudyIdsNull_AllowsEveryStudy()
{
    var context = TestDbContextFactory.Create();
    var service = new ShipmentService(context, new AuditService(context));

    var study = new Study { StudyCode = "SHIP-ANY" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();
    context.Specimens.Add(new Specimen { BarcodeID = "SHIP-S2", StudyID = study.StudyID, Status = "In-Stock" });
    await context.SaveChangesAsync();

    var csv = "Barcode\nSHIP-S2";
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

    var batch = await service.ImportBatchFromCsvAsync(stream, "test-user", null, null, null);

    Assert.Equal(1, batch.TotalAvailable);
    Assert.Equal(0, batch.TotalNoAccess);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ShipmentServiceStudyEnforcementTests"`
Expected: compile error (no 5-parameter overload / no `TotalNoAccess` property yet) — this is
expected at this stage; proceed to Step 3.

- [ ] **Step 3: Add `TotalNoAccess`** to `STASIS/Models/ShipmentBatch.cs`, next to the existing
  `TotalNotFound`/`TotalPreviouslyShipped`/`TotalDiscarded`/`TotalNotYetReceived` properties:

```csharp
public int TotalNoAccess { get; set; }
```

- [ ] **Step 4: Update `IShipmentService.ImportBatchFromCsvAsync`'s signature** in
  `STASIS/Services/IShipmentService.cs`:

```csharp
Task<ShipmentBatch> ImportBatchFromCsvAsync(Stream csvStream, string userId, string? requestorName, string? requestorEmail, IReadOnlyCollection<int>? editableStudyIds);
```

- [ ] **Step 5: Implement the per-row check in `ShipmentService.ImportBatchFromCsvAsync`**,
  inserting a new branch right after the existing `specimen == null` check (~line 95 in the
  current file) and before the `Shipped`/`Discarded`/`Not Yet Received` branches — a caller who
  can't edit the specimen's study should never see its real status:

```csharp
if (specimen == null)
{
    status = "Not Found";
    totalNotFound++;
}
else if (editableStudyIds != null && !editableStudyIds.Contains(specimen.StudyID))
{
    status = "No Access";
    totalNoAccess++;
}
else if (specimen.Status == "Shipped")
{
    // ... unchanged
```

Add `int totalNoAccess = 0;` alongside the other counters near the top of the method, and
`batch.TotalNoAccess = totalNoAccess;` alongside the other `batch.Total* = ...` assignments before
`SaveChangesAsync`.

- [ ] **Step 6: Run the service-level tests to verify they pass**

Run: same filter as Step 2.
Expected: PASS.

- [ ] **Step 7: Write the failing page-level test** in new file
  `STASIS.Tests/Integration/ShipmentsCreateAccessTests.cs`, following
  `AddSampleAccessTests.cs`'s class structure and this plan's established
  `CreateSystemAdminAsync`/`CreateUserAsync`/`GrantAsync` helper bodies:

```csharp
[Fact]
public async Task OnPostAsync_CallerIsEditorOnAtLeastOneStudy_DoesNotReturnForbid()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ship1@test.com");
    var study = new Study { StudyCode = "SHIP-PAGE-EDIT" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    var caller = await CreateUserAsync(services, "editor-ship1@test.com");
    await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

    var model = BuildCreateModel(services, caller.Id);
    var csvBytes = System.Text.Encoding.UTF8.GetBytes("Barcode\nSHIP-PAGE-001");
    model.CsvFile = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "CsvFile", "shipments.csv");

    var result = await model.OnPostAsync();

    Assert.IsNotType<ForbidResult>(result);
}

[Fact]
public async Task OnPostAsync_CallerWithNoEditorAccessAnywhere_ReturnsForbid()
{
    using var scope = _factory.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StasisDbContext>();

    var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ship2@test.com");
    var study = new Study { StudyCode = "SHIP-PAGE-VIEWONLY" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    var caller = await CreateUserAsync(services, "viewer-ship2@test.com");
    await GrantAsync(context, caller.Id, study.StudyID, "Viewer", sysAdmin.Id);

    var model = BuildCreateModel(services, caller.Id);
    var csvBytes = System.Text.Encoding.UTF8.GetBytes("Barcode\nSHIP-PAGE-002");
    model.CsvFile = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "CsvFile", "shipments.csv");

    var result = await model.OnPostAsync();

    Assert.IsType<ForbidResult>(result);
}
```

Add a `BuildCreateModel` helper to this new file, copying the `PageContext`/`TempData` wiring from
`RemainingWriteGatesTests.cs`'s `BuildCreateModel`/`BuildPlaceModel` (same shape, constructing
`STASIS.Pages.Shipments.CreateModel` instead).

- [ ] **Step 8: Run the page-level tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ShipmentsCreateAccessTests"`
Expected: FAIL — `[Authorize(Roles = "Admin")]` forbids the Editor caller too (both tests may
currently fail or one may not compile against the still-4-parameter service call; proceed to Step 9).

- [ ] **Step 9: Fix `Pages/Shipments/Create.cshtml.cs`** — remove the class-level `[Authorize]`,
  add an `IsEditorAnywhereAsync` gate (same body as `Boxes/Create.cshtml.cs`'s — this duplication
  is intentional per this plan's Global Constraints; a later cleanup pass can consolidate the five
  existing copies this review found, this task isn't the place to do that), resolve
  `editableStudyIds` the same way `Pages/Samples/Import.cshtml.cs`'s `ResolveEditableStudyIdsAsync`
  does, and pass it through:

```csharp
public class CreateModel : PageModel
{
    private readonly IShipmentService _shipmentService;
    private readonly ISampleService _sampleService;
    private readonly IStudyAccessService _studyAccessService;

    public CreateModel(IShipmentService shipmentService, ISampleService sampleService, IStudyAccessService studyAccessService)
    {
        _shipmentService = shipmentService;
        _sampleService = sampleService;
        _studyAccessService = studyAccessService;
    }

    // ... BindProperty fields unchanged ...

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var (isSystemAdmin, editableStudyIds) = await ResolveEditableStudyIdsAsync(userId);
        if (!isSystemAdmin && editableStudyIds!.Count == 0)
        {
            return Forbid();
        }

        if (CsvFile == null || CsvFile.Length == 0)
        {
            ModelState.AddModelError("CsvFile", "Please select a CSV file.");
            return Page();
        }

        using var stream = CsvFile.OpenReadStream();
        ImportedBatch = await _shipmentService.ImportBatchFromCsvAsync(
            stream, userId, RequestorName, RequestorEmail, isSystemAdmin ? null : editableStudyIds);

        return Page();
    }

    // Same body as Pages/Samples/Import.cshtml.cs's ResolveEditableStudyIdsAsync.
    private async Task<(bool IsSystemAdmin, IReadOnlyCollection<int>? EditableStudyIds)> ResolveEditableStudyIdsAsync(string userId)
    {
        var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);
        if (isSystemAdmin)
        {
            return (true, null);
        }

        var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(userId);
        var candidateStudies = await _sampleService.GetAllStudies(visibleStudyIds);

        var editableStudyIds = new List<int>();
        foreach (var study in candidateStudies)
        {
            if (await _studyAccessService.CanEditAsync(userId, study.StudyID))
            {
                editableStudyIds.Add(study.StudyID);
            }
        }

        return (false, editableStudyIds);
    }
}
```

Remove the now-unused `using Microsoft.AspNetCore.Authorization;` line.

- [ ] **Step 10: Run the page-level tests to verify they pass**

Run: same filter as Step 8.
Expected: PASS.

- [ ] **Step 11: Run the full test suite**

Run: `STASIS_TEST_CONNECTION=... dotnet test`
Expected: all PASS.

- [ ] **Step 12: Commit**

```bash
git add STASIS/Models/ShipmentBatch.cs STASIS/Services/IShipmentService.cs \
  STASIS/Services/ShipmentService.cs Pages/Shipments/Create.cshtml.cs \
  STASIS.Tests/Services/ShipmentServiceStudyEnforcementTests.cs \
  STASIS.Tests/Integration/ShipmentsCreateAccessTests.cs
git commit -m "Replaced Shipments/Create's Admin-only gate with per-row study-scoped access"
```

---

## Task 9: Reuse GetManageableStudiesAsync instead of three hand-rolled authorization loops

**Files:**
- Modify: `Pages/Administration/Users.cshtml.cs` (`OnGetAsync`, ~line 38-66)
- Modify: `Pages/Administration/CreateUser.cshtml.cs` (`AuthorizeCallerAsync`, ~line 173-202)
- Modify: `Pages/Administration/EditUser.cshtml.cs` (`AuthorizeCallerAsync`, ~line 318-344)
- Test: existing tests in `UsersPageTests.cs`, `CreateUserTests.cs`, `EditUserAccessTests.cs`
  must keep passing unmodified (this task is a pure refactor — same observable behavior, fewer
  queries)

- [ ] **Step 1: Run the existing test suite first** to capture the current baseline

Run: `dotnet test --filter "FullyQualifiedName~UsersPageTests|FullyQualifiedName~CreateUserTests|FullyQualifiedName~EditUserAccessTests"`
Expected: all PASS (establishes the behavior this refactor must not change).

- [ ] **Step 2: Replace the loop in `Users.cshtml.cs`'s `OnGetAsync`**:

```csharp
var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(currentUserId);

var managedStudyIds = new HashSet<int>();
if (!isSystemAdmin)
{
    var manageableStudies = await _studyAccessService.GetManageableStudiesAsync(currentUserId);
    managedStudyIds = manageableStudies.Select(s => s.StudyID).ToHashSet();

    if (managedStudyIds.Count == 0)
    {
        return Forbid();
    }
}
```

- [ ] **Step 3: Replace the identical loop in `CreateUser.cshtml.cs`'s `AuthorizeCallerAsync`**:

```csharp
private async Task<CallerAuthorization> AuthorizeCallerAsync(string currentUserId)
{
    var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(currentUserId);

    var managedStudyIds = new HashSet<int>();
    if (!isSystemAdmin)
    {
        var manageableStudies = await _studyAccessService.GetManageableStudiesAsync(currentUserId);
        managedStudyIds = manageableStudies.Select(s => s.StudyID).ToHashSet();

        if (managedStudyIds.Count == 0)
        {
            return new CallerAuthorization(false, managedStudyIds, true);
        }
    }

    return new CallerAuthorization(isSystemAdmin, managedStudyIds, false);
}
```

- [ ] **Step 4: Replace the identical loop in `EditUser.cshtml.cs`'s `AuthorizeCallerAsync`**
  with the same body as Step 3.

- [ ] **Step 5: Run the same tests as Step 1 to confirm no behavior change**

Run: same filter as Step 1.
Expected: all PASS, identical results to the Step 1 baseline.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add Pages/Administration/Users.cshtml.cs Pages/Administration/CreateUser.cshtml.cs \
  Pages/Administration/EditUser.cshtml.cs
git commit -m "Reused GetManageableStudiesAsync instead of three duplicated authorization loops"
```

---

## Task 10: Stop EditUser from scanning UserStudyAccess twice per page load

**Files:**
- Modify: `Pages/Administration/EditUser.cshtml.cs` (`OnGetAsync`, `OnPostAsync`,
  `LoadAccessRowsAsync`, `BuildGrantableStudySelectListAsync`)
- Test: existing tests in `EditUserAccessTests.cs` must keep passing unmodified (pure refactor)

**Interfaces:**
- Produces: `LoadAccessRowsAsync(string userId, CallerAuthorization authorization, List<UserStudyAccess> allAccess)`
  and `BuildGrantableStudySelectListAsync(string currentUserId, string targetUserId, List<UserStudyAccess> allAccess)`
  — both gain a third parameter; both call sites (in `OnGetAsync` and in `OnPostAsync`'s
  `!ModelState.IsValid` branch) must be updated together in this same task.

- [ ] **Step 1: Run `EditUserAccessTests.cs` first** to capture the baseline

Run: `dotnet test --filter "FullyQualifiedName~EditUserAccessTests"`
Expected: all PASS.

- [ ] **Step 2: Change `LoadAccessRowsAsync`'s signature** to accept the already-fetched list
  instead of calling `GetAllAccessRowsAsync()` itself:

```csharp
private Task LoadAccessRowsAsync(string userId, CallerAuthorization authorization, List<UserStudyAccess> allAccess)
{
    var rows = allAccess.Where(a => a.AspNetUserId == userId);

    if (!authorization.IsSystemAdmin)
    {
        rows = rows.Where(a => authorization.ManagedStudyIds.Contains(a.StudyID));
    }

    AccessRows = rows
        .Select(a => new AccessRowViewModel
        {
            StudyID = a.StudyID,
            StudyCode = a.Study?.StudyCode ?? string.Empty,
            Role = a.Role,
            CallerCanManage = authorization.IsSystemAdmin || authorization.ManagedStudyIds.Contains(a.StudyID)
        })
        .OrderBy(a => a.StudyCode)
        .ToList();

    return Task.CompletedTask;
}
```

(No longer `async` since it no longer awaits anything — update both call sites to drop `await`
if the compiler requires it, or keep it `async Task` with no `await` if the project's existing
style tolerates that warning; match whatever `LoadAccessRowsAsync`'s sibling private methods in
this file already do.)

- [ ] **Step 3: Change `BuildGrantableStudySelectListAsync`'s signature**:

```csharp
private async Task<SelectList> BuildGrantableStudySelectListAsync(string currentUserId, string targetUserId, List<UserStudyAccess> allAccess)
{
    var manageableStudies = await _studyAccessService.GetManageableStudiesAsync(currentUserId);

    var alreadyGrantedStudyIds = allAccess
        .Where(a => a.AspNetUserId == targetUserId)
        .Select(a => a.StudyID)
        .ToHashSet();

    var grantable = manageableStudies.Where(s => !alreadyGrantedStudyIds.Contains(s.StudyID));
    return new SelectList(grantable, "StudyID", "StudyCode");
}
```

- [ ] **Step 4: Update both call sites** — `OnGetAsync` and `OnPostAsync`'s
  `!ModelState.IsValid` branch — to fetch `GetAllAccessRowsAsync()` once and pass it to both
  methods:

```csharp
var allAccess = await _studyAccessService.GetAllAccessRowsAsync();
await LoadAccessRowsAsync(user.Id, authorization, allAccess);
GrantableStudies = await BuildGrantableStudySelectListAsync(currentUserId, user.Id, allAccess);
```

(In `OnPostAsync`, use `Input.UserId` in place of `user.Id` — match whichever variable the
existing code in that branch already uses.)

- [ ] **Step 5: Run `EditUserAccessTests.cs` to confirm no behavior change**

Run: same filter as Step 1.
Expected: all PASS, identical results to the Step 1 baseline.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: all PASS (108+ tests from Tasks 1-9, unaffected by this one).

- [ ] **Step 7: Commit**

```bash
git add Pages/Administration/EditUser.cshtml.cs
git commit -m "Fetched UserStudyAccess rows once per EditUser page load instead of twice"
```

---

## Final Step: Whole-branch verification

- [ ] Run `dotnet build STASIS.sln` — expect 0 errors.
- [ ] Run `STASIS_TEST_CONNECTION=... dotnet test` — expect all tests passing.
- [ ] Re-run the two live exploit checks from the original review against a fresh local instance:
  a zero-access user `GET /Boxes/Place?BoxLabel=<any box>` must not return box/specimen detail;
  an Editor on study A must get `Forbid()` attempting to `POST` a move for a study-B box.
- [ ] Hand off to `superpowers:requesting-code-review` for a fresh whole-branch review before
  opening the PR.

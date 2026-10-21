# Study-Scoped Access Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace STASIS's global Write/Read roles with per-study access
(Viewer/Editor/Study Admin), add a System Admin bypass, enforce it
server-side on every study-scoped read and write, and fix the box/study
mixing bug that made per-box filtering possible in the first place.

**Architecture:** A new `tbl_UserStudyAccess` join table and a `StudyID`
column on `tbl_Boxes` are the data model. A new `IStudyAccessService`
centralizes role resolution and is consulted everywhere a study-scoped
decision is made — admin UI (who can grant what), read filtering (what
shows up in lists/search), and write validation (server-side re-check on
every mutating handler, never trusting the UI). Work is ordered so the
admin UI for granting access ships before enforcement is switched on, so
no committed state leaves the app unusable for non-admin users.

**Tech Stack:** .NET 10 Razor Pages, EF Core (Npgsql), ASP.NET Core
Identity, xUnit, `Microsoft.EntityFrameworkCore.InMemory` (unit tests），
`Microsoft.AspNetCore.Mvc.Testing` (integration tests).

**Spec:** `docs/superpowers/specs/2026-10-21-study-scoped-access-control-design.md`

## Global Constraints

- Page models must never call `StasisDbContext` directly — only through
  `ISampleService`/`IStorageService`/`IStudyAccessService`/etc.
- Table naming convention is `tbl_X`. New table: `tbl_UserStudyAccess`.
- Schema changes go through `dotnet ef migrations add <Name> --project STASIS.csproj`,
  one migration per task that changes schema.
- Access is purely additive — no deny rules, no default-all fallback for
  anyone except System Admin.
- A study's assignment table role is one of exactly `"Viewer"`, `"Editor"`,
  `"StudyAdmin"` (check constraint). `StudyAdmin` implies Editor-level
  content rights.
- Removing the last `StudyAdmin` on a study is blocked for everyone,
  including System Admin (System Admin can always *add* a new Study Admin
  to a zero-admin study — that path is never blocked).
- System Admin override actions (writes performed on a study the System
  Admin isn't personally assigned to) are logged via the existing
  `IAuditService`, not a new mechanism.
- CSV import rejects per row (consistent with existing `ErrorRows`/
  `SuccessRows` handling in `ImportSpecimensFromCsv`), not the whole file,
  when a row's study is outside the uploader's Editor access.

## Review Focus

- **Zero-access user hits a study-scoped page directly by URL or form
  post** (e.g. `/Samples/Detail?id=123` for a specimen in a study they
  were never assigned to, or posting `Add` with a tampered `StudyID`).
  Expected: 403/404, never the data. This is the core anti-goal from the
  spec and the single most likely thing to regress silently.
- **A box already holding specimens for Study A receives a move/rebox/add
  targeting Study B.** Expected: rejected with a clear error, not a
  silent mixed box (the confirmed bug this plan is also fixing).
- **A Study Admin tries to demote/remove the last remaining Study Admin on
  their own study** (including trying to remove themselves). Expected:
  blocked, with a clear message — the spec calls this out explicitly as a
  guardrail, not an edge case to discover later.
- **A CSV import file has some rows for a study the uploader can edit and
  some for a study they can't.** Expected: the editable rows still import;
  the others land in `ErrorRows` with a reason, not a whole-file rejection
  and not a silent cross-study import.
- **Access is revoked while the user has an active session/cookie.**
  Expected: the change is visible on their very next request, not only
  after they log in again — this depends on the `SecurityStampValidator`
  interval, which defaults to something coarser than "next request" and
  must be tightened.

---

## Phase 1 — Schema

### Task 1: `UserStudyAccess` model, EF configuration, migration, and test project

**Files:**
- Create: `STASIS.Tests/STASIS.Tests.csproj`
- Create: `STASIS/Models/UserStudyAccess.cs`
- Modify: `STASIS/Data/StasisDbContext.cs`
- Modify: `STASIS.sln` (add test project)
- Create: `Migrations/<timestamp>_AddUserStudyAccess.cs` (generated)
- Test: `STASIS.Tests/Models/UserStudyAccessConfigurationTests.cs`

**Interfaces:**
- Produces: `STASIS.Models.UserStudyAccess` with properties `UserStudyAccessID (int)`,
  `AspNetUserId (string)`, `User (IdentityUser?)`, `StudyID (int)`,
  `Study (Study?)`, `Role (string)`, `GrantedByUserId (string)`,
  `GrantedByUser (IdentityUser?)`, `GrantedDate (DateTime)`.
- Produces: `StasisDbContext.UserStudyAccesses` (`DbSet<UserStudyAccess>`).

- [ ] **Step 1: Create the `STASIS.Tests` xUnit project**

```bash
dotnet new xunit -o STASIS.Tests
dotnet add STASIS.Tests/STASIS.Tests.csproj reference STASIS.csproj
dotnet add STASIS.Tests/STASIS.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 10.0.1
dotnet sln STASIS.sln add STASIS.Tests/STASIS.Tests.csproj
```

- [ ] **Step 2: Write a failing EF configuration test**

```csharp
// STASIS.Tests/Models/UserStudyAccessConfigurationTests.cs
[Fact]
public async Task UserStudyAccess_RejectsDuplicateUserStudyPair()
{
    var context = TestDbContextFactory.Create(); // see note below
    context.UserStudyAccesses.Add(new UserStudyAccess { AspNetUserId = "u1", StudyID = 1, Role = "Viewer", GrantedByUserId = "admin", GrantedDate = DateTime.UtcNow });
    await context.SaveChangesAsync();

    context.UserStudyAccesses.Add(new UserStudyAccess { AspNetUserId = "u1", StudyID = 1, Role = "Editor", GrantedByUserId = "admin", GrantedDate = DateTime.UtcNow });
    await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
}
```

`TestDbContextFactory.Create()` is a small helper you write alongside this
test: builds a `StasisDbContext` with `UseInMemoryDatabase(Guid.NewGuid().ToString())`.
The EF Core InMemory provider does enforce unique indexes, so this test is
valid against it; check constraints are not enforced by InMemory, so the
`Role` check constraint is verified later in a Postgres-backed integration
test (Task 15), not here.

- [ ] **Step 3: Run test, confirm it fails** (type doesn't exist yet)

Run: `dotnet test STASIS.Tests` — expect a compile error.

- [ ] **Step 4: Implement `UserStudyAccess` and wire it into `StasisDbContext`**

Add the model (plain properties listed above, mirroring `UserProfile.cs`'s
style for the `[Key]`/`[ForeignKey]` or fluent-API choice — this codebase
uses fluent API in `OnModelCreating` for everything except `UserProfile`,
so configure this one fluently to match the dominant pattern). In
`OnModelCreating`:

```csharp
modelBuilder.Entity<UserStudyAccess>().ToTable("tbl_UserStudyAccess");

modelBuilder.Entity<UserStudyAccess>()
    .HasIndex(a => new { a.AspNetUserId, a.StudyID })
    .IsUnique();

modelBuilder.Entity<UserStudyAccess>()
    .HasIndex(a => a.StudyID);

modelBuilder.Entity<UserStudyAccess>()
    .ToTable(t => t.HasCheckConstraint(
        "CK_UserStudyAccess_Role",
        "\"Role\" IN ('Viewer', 'Editor', 'StudyAdmin')"));

modelBuilder.Entity<UserStudyAccess>()
    .Property(a => a.GrantedDate)
    .HasDefaultValueSql("CURRENT_TIMESTAMP");

modelBuilder.Entity<UserStudyAccess>()
    .HasOne(a => a.User)
    .WithMany()
    .HasForeignKey(a => a.AspNetUserId)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<UserStudyAccess>()
    .HasOne(a => a.GrantedByUser)
    .WithMany()
    .HasForeignKey(a => a.GrantedByUserId)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<UserStudyAccess>()
    .HasOne(a => a.Study)
    .WithMany()
    .HasForeignKey(a => a.StudyID)
    .OnDelete(DeleteBehavior.Cascade);
```

Two FKs to `AspNetUsers` from one entity already has precedent in this
codebase — see `Approval`'s four approver FKs in `StasisDbContext.cs`.

- [ ] **Step 5: Run test, confirm it passes**

Run: `dotnet test STASIS.Tests` — expect PASS.

- [ ] **Step 6: Generate and apply the migration**

```bash
dotnet ef migrations add AddUserStudyAccess --project STASIS.csproj
dotnet ef database update --project STASIS.csproj
```

- [ ] **Step 7: Commit**

```bash
git add STASIS.Tests STASIS.sln STASIS/Models/UserStudyAccess.cs STASIS/Data/StasisDbContext.cs Migrations
git commit -m "Add UserStudyAccess table and test project"
```

### Task 2: `Box.StudyID` column and EF configuration

**Files:**
- Modify: `STASIS/Models/Box.cs`
- Modify: `STASIS/Data/StasisDbContext.cs`
- Create: `Migrations/<timestamp>_AddBoxStudyID.cs` (generated)
- Test: `STASIS.Tests/Models/BoxStudyConfigurationTests.cs`

**Interfaces:**
- Produces: `Box.StudyID (int?)`, `Box.Study (Study?)`.

- [ ] **Step 1: Write a failing test** — a `Box` can be saved with a null
  `StudyID`, and with a real one, and querying `Box.Study` navigates
  correctly:

```csharp
[Fact]
public async Task Box_StudyID_IsOptionalAndNavigates()
{
    var context = TestDbContextFactory.Create();
    var study = new Study { StudyCode = "TEST" };
    context.Studies.Add(study);
    await context.SaveChangesAsync();

    var box = new Box { BoxLabel = "B1", BoxType = "81-slot", StudyID = study.StudyID };
    context.Boxes.Add(box);
    await context.SaveChangesAsync();

    var reloaded = await context.Boxes.Include(b => b.Study).FirstAsync(b => b.BoxID == box.BoxID);
    Assert.Equal("TEST", reloaded.Study!.StudyCode);
}
```

- [ ] **Step 2: Run test, confirm it fails** (property doesn't exist)

- [ ] **Step 3: Add `StudyID`/`Study` to `Box.cs` and configure in `OnModelCreating`**

```csharp
modelBuilder.Entity<Box>()
    .HasOne(b => b.Study)
    .WithMany()
    .HasForeignKey(b => b.StudyID)
    .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 4: Run test, confirm it passes**

- [ ] **Step 5: Generate and apply the migration**

```bash
dotnet ef migrations add AddBoxStudyID --project STASIS.csproj
dotnet ef database update --project STASIS.csproj
```

- [ ] **Step 6: Commit**

---

## Phase 2 — Core authorization service

### Task 3: `StudyRole` enum and `IStudyAccessService`

**Files:**
- Create: `STASIS/Services/StudyRole.cs`
- Create: `STASIS/Services/IStudyAccessService.cs`
- Create: `STASIS/Services/StudyAccessService.cs`
- Modify: `Program.cs` (register `IStudyAccessService`)
- Test: `STASIS.Tests/Services/StudyAccessServiceTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum StudyRole { None = 0, Viewer = 1, Editor = 2, StudyAdmin = 3 }

  public interface IStudyAccessService
  {
      Task<bool> IsSystemAdminAsync(string userId);
      Task<StudyRole> GetRoleAsync(string userId, int studyId);
      Task<IReadOnlyCollection<int>?> GetVisibleStudyIdsAsync(string userId); // null = unrestricted (System Admin)
      Task<bool> CanViewAsync(string userId, int studyId);
      Task<bool> CanEditAsync(string userId, int studyId); // Editor or StudyAdmin or System Admin
      Task<bool> CanManageStudyAsync(string userId, int studyId); // StudyAdmin or System Admin
  }
  ```
- Consumes: `StasisDbContext.UserStudyAccesses` (Task 1), `UserManager<IdentityUser>`
  (existing, for the `"Admin"` role check).

- [ ] **Step 1: Write failing tests covering the role matrix**

```csharp
// STASIS.Tests/Services/StudyAccessServiceTests.cs
[Fact] public async Task SystemAdmin_IsVisibleEverywhere_WithoutAssignment();
[Fact] public async Task Viewer_CanView_ButNotEdit();
[Fact] public async Task Editor_CanViewAndEdit();
[Fact] public async Task StudyAdmin_CanEditAndManage();
[Fact] public async Task NoAssignment_CannotViewOrEdit_AndIsNotSystemAdmin();
[Fact] public async Task GetVisibleStudyIdsAsync_ReturnsNull_ForSystemAdmin();
[Fact] public async Task GetVisibleStudyIdsAsync_ReturnsExactAssignedSet_ForStaff();
```

Each test seeds an in-memory `StasisDbContext` and (for the System Admin
cases) a `UserManager<IdentityUser>` backed by the same context with the
user placed in Identity role `"Admin"`. Assert the exact `StudyRole` value
and the exact boolean each `CanXAsync` method returns for each case.

- [ ] **Step 2: Run tests, confirm they fail** (types don't exist)

- [ ] **Step 3: Implement `StudyAccessService`**

Constructor takes `StasisDbContext` and `UserManager<IdentityUser>`.
`IsSystemAdminAsync` delegates to `userManager.IsInRoleAsync(user, "Admin")`
after loading the user via `userManager.FindByIdAsync(userId)`.
`GetRoleAsync` queries `UserStudyAccesses` for the `(userId, studyId)` pair
and maps the `Role` string to the enum (`"StudyAdmin"` → `StudyRole.StudyAdmin`,
etc.), `StudyRole.None` if no row. `GetVisibleStudyIdsAsync` returns `null`
if `IsSystemAdminAsync` is true, else the set of `StudyID` from that user's
`UserStudyAccesses` rows (possibly empty). `CanViewAsync` is true if
`IsSystemAdminAsync` or `GetRoleAsync(...) != StudyRole.None`. `CanEditAsync`
is true if `IsSystemAdminAsync` or `GetRoleAsync(...) >= StudyRole.Editor`.
`CanManageStudyAsync` is true if `IsSystemAdminAsync` or
`GetRoleAsync(...) == StudyRole.StudyAdmin`.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Register in `Program.cs`**

```csharp
builder.Services.AddScoped<IStudyAccessService, StudyAccessService>();
```

- [ ] **Step 6: Commit**

---

## Phase 3 — Role retirement and the admin UI to grant access

This phase ships before enforcement is switched on (Phase 4/5), so there is
always a way to grant access before anything starts requiring it.

### Task 4: Stop seeding global Write/Read roles

**Files:**
- Modify: `Program.cs:85` (the `foreach (var role in new[] { "Admin", "Write", "Read" })` block)
- Create: `STASIS.Tests/TestWebApplicationFactory.cs`
- Test: `STASIS.Tests/Integration/AdminSeedTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `STASIS.Tests.TestWebApplicationFactory : WebApplicationFactory<Program>` —
  a shared fixture, reused by every later integration/security test task
  (9, 10, 12, 13, 14, 15) via xUnit's `IClassFixture<TestWebApplicationFactory>`.
  This is the first task in the plan that needs a `WebApplicationFactory`,
  so it owns creating it; no later task may create a second one — they
  consume this file.

- [ ] **Step 1: Create the shared `TestWebApplicationFactory`.** It points
  at a throwaway Postgres test database via a connection string read from
  the `STASIS_TEST_CONNECTION` environment variable (document this
  requirement in a comment on the class), overrides the app's
  `StasisDbContext` registration to use that connection string, and runs
  `context.Database.Migrate()` plus a data-reset (delete all rows in
  dependency order, or recreate the schema — your call, document whichever
  you pick) in a way each test can call between tests to avoid
  cross-test pollution.

- [ ] **Step 2: Write a failing integration test** asserting that after
  startup, Identity role `"Write"` and `"Read"` are not created (only
  `"Admin"` is), using `TestWebApplicationFactory`.

- [ ] **Step 3: Run test, confirm it fails**

- [ ] **Step 4: Change the seeded role list to `new[] { "Admin" }`**

Leave any already-existing `"Write"`/`"Read"` rows in `AspNetRoles`
untouched — per the design doc, cleanup is optional, not required.

- [ ] **Step 5: Run test, confirm it passes**

- [ ] **Step 6: Commit**

### Task 5: Scope the Administration/Users list

**Files:**
- Modify: `Pages/Administration/Users.cshtml.cs`
- Modify: `Pages/Administration/Users.cshtml`
- Test: `STASIS.Tests/Integration/UsersPageTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.IsSystemAdminAsync`, `GetVisibleStudyIdsAsync` (Task 3).
- Produces: `UsersModel.Users` now includes each user's study assignments
  (replace the single `Roles` string with a list of `(StudyCode, Role)`).

- [ ] **Step 1: Write failing tests**: a System Admin sees every user; a
  Study Admin for Study A sees only users who have *some* access row on
  Study A (plus themselves); a plain Viewer/Editor gets `Forbid()` (this
  page is for System Admins and Study Admins only, never plain staff).

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Remove `[Authorize(Roles = "Admin")]`; at the
  top of `OnGetAsync`, resolve the caller's `IsSystemAdminAsync` and the
  set of studies where `CanManageStudyAsync` is true. If neither, `return Forbid();`
  equivalent — change `OnGetAsync` to `Task<IActionResult> OnGetAsync()`.
  Change `UserViewModel.Roles` (string) to `UserViewModel.StudyAccess`
  (`List<(string StudyCode, string Role)>`), populated from
  `UserStudyAccesses` joined to `Studies`, filtered to the managed-study set
  for a Study Admin (unfiltered for System Admin). Update the `.cshtml` to
  render the list instead of the old `Roles` string.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 6: `CreateUser` — System Admin flag and first study assignment

**Files:**
- Modify: `Pages/Administration/CreateUser.cshtml.cs`
- Modify: `Pages/Administration/CreateUser.cshtml`
- Test: `STASIS.Tests/Integration/CreateUserTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService` (Task 3).
- Produces: `CreateUserModel.InputModel` gains `bool IsSystemAdmin`,
  `int? StudyID`, `string? StudyRole` (replacing the old `string Role`
  Identity-role field).

- [ ] **Step 1: Write failing tests**: System Admin creating a user can set
  `IsSystemAdmin = true` (ends up with the `"Admin"` Identity role) and/or
  assign a study+role (creates a `UserStudyAccess` row with
  `GrantedByUserId` = the creator); a Study Admin creating a user cannot
  set `IsSystemAdmin` (page rejects/ignores it) and can only pick a study
  they personally manage; posting a `StudyID` the caller doesn't manage is
  rejected server-side even if the dropdown wouldn't have offered it (IDOR
  check per the spec's anti-goals).

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Remove `[Authorize(Roles = "Admin")]`, replace
  with: allow if `IsSystemAdminAsync` or `GetVisibleStudyIdsAsync` contains
  at least one study where `CanManageStudyAsync` is true; else `Forbid()`.
  Replace the `Roles` `SelectList` (all Identity roles) with a `SelectList`
  of studies the caller manages (all studies if System Admin). On post:
  create the `IdentityUser` as before; if `Input.IsSystemAdmin` and caller
  `IsSystemAdminAsync`, `AddToRoleAsync(user, "Admin")`; if `Input.StudyID`
  has a value, re-validate `CanManageStudyAsync(callerId, Input.StudyID.Value)`
  server-side, then insert a `UserStudyAccess` row with
  `GrantedByUserId = callerId`. Audit-log the access grant via the existing
  `IAuditService` (table `"tbl_UserStudyAccess"`).

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 7: `EditUser` — manage-access table

**Files:**
- Modify: `Pages/Administration/EditUser.cshtml.cs`
- Modify: `Pages/Administration/EditUser.cshtml`
- Test: `STASIS.Tests/Integration/EditUserAccessTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.CanManageStudyAsync`, `GetRoleAsync` (Task 3).
- Produces: new handlers `OnPostGrantAccessAsync(string userId, int studyId, string role)`,
  `OnPostRevokeAccessAsync(string userId, int studyId)`,
  `OnPostChangeRoleAsync(string userId, int studyId, string newRole)`.

- [ ] **Step 1: Write failing tests**:
  - Granting access the caller doesn't manage (`studyId` outside
    `CanManageStudyAsync`) is rejected server-side.
  - Revoking/demoting the last `StudyAdmin` on a study is rejected with a
    clear error and the row is unchanged, including when the caller is a
    System Admin.
  - Revoking/demoting succeeds when at least one other `StudyAdmin` remains
    on that study.
  - Every grant/revoke/role-change writes an `IAuditService` entry.

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Remove the old single-`Role`-dropdown
  Identity-role editing entirely (global Write/Read is retired). Replace
  the `Input.Role` field with the three new handlers above. Each handler:
  re-validates `CanManageStudyAsync(callerId, studyId)` server-side first;
  `OnPostRevokeAccessAsync`/`OnPostChangeRoleAsync` additionally check — if
  the target row's current role is `StudyAdmin` and the new state would
  leave zero `StudyAdmin` rows for that `studyId` — reject with
  `TempData["Error"] = "A study must have at least one Study Admin."`
  before making any change. On success, write the `IAuditService` entry
  and `RedirectToPage()`. Update `.cshtml` to render a confirmation prompt
  (a plain "are you sure?" submit, no new JS framework needed — match the
  existing Bootstrap form style used elsewhere) before revoke/demote posts.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 8: `LabSetup/Studies` — designate the first Study Admin at creation

**Files:**
- Modify: `Pages/LabSetup/Studies.cshtml.cs`
- Modify: `Pages/LabSetup/Studies.cshtml`
- Test: `STASIS.Tests/Integration/StudyCreationTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.IsSystemAdminAsync` (Task 3).
- Produces: `StudyInput` gains `string FirstStudyAdminUserId`.

- [ ] **Step 1: Write failing tests**: creating a study with a chosen
  `FirstStudyAdminUserId` creates the study and a `UserStudyAccess` row
  with `Role = "StudyAdmin"` for that user in the same request; the System
  Admin who created the study does **not** get an implicit access row
  (per the design doc's explicit call — they stay in override status
  unless they separately grant themselves access); only System Admins can
  reach this page at all (page keeps `[Authorize(Roles = "Admin")]` —
  `"Admin"` still means System Admin post-retirement).

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Add a required `FirstStudyAdminUserId` field
  (a `SelectList` of all `AspNetUsers`, since picking who becomes the first
  admin of a brand-new study is necessarily unscoped). In
  `OnPostAddAsync`, after `SaveChangesAsync` creates the study, insert the
  `UserStudyAccess` row (`GrantedByUserId` = the caller) and audit-log it.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

---

## Phase 4 — Read-side enforcement

### Task 9: Samples search and detail pages

**Files:**
- Modify: `STASIS/Services/ISampleService.cs`, `STASIS/Services/SampleService.cs`
  (`GetSpecimensAsync`, `GetAllStudies` gain an `IReadOnlyCollection<int>? visibleStudyIds` parameter)
- Modify: `Pages/Samples.cshtml.cs`, `Pages/Samples/Detail.cshtml.cs`
- Test: `STASIS.Tests/Integration/SamplesVisibilityTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.GetVisibleStudyIdsAsync`, `CanViewAsync` (Task 3).
- Produces: `ISampleService.GetSpecimensAsync(string?, int?, int?, string?, int, int, IReadOnlyCollection<int>? visibleStudyIds)`,
  `ISampleService.GetAllStudies(IReadOnlyCollection<int>? visibleStudyIds)`.

- [ ] **Step 1: Write failing tests**: a Viewer on Study A searching
  Samples never sees a Study B specimen even with no filters applied; the
  Study dropdown only lists Study A; requesting `/Samples/Detail?id=`
  (a Study B specimen's id) returns `NotFound()`; a System Admin sees
  both.

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Add the `visibleStudyIds` parameter to both
  `ISampleService` methods; when non-null, add
  `query = query.Where(s => visibleStudyIds.Contains(s.StudyID))` (and the
  equivalent for `GetAllStudies`). In `SamplesModel.OnGetAsync`, resolve
  `GetVisibleStudyIdsAsync` first and pass it through. In
  `DetailModel.OnGetAsync`, after loading the specimen, call
  `CanViewAsync(userId, specimen.StudyID)`; if false, `return NotFound();`
  (not `Forbid()` — don't reveal the record exists).

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 10: Box search page

**Files:**
- Modify: `STASIS/Services/IStorageService.cs`, `STASIS/Services/StorageService.cs`
  (`SearchBoxesAsync`, `GetBoxWithSpecimens`, `GetBoxByLabelAsync`,
  `GetAllBoxesAsync`, `GetAllBoxLabelsAsync` gain the same
  `IReadOnlyCollection<int>? visibleStudyIds` parameter)
- Modify: `Pages/Boxes/Search.cshtml.cs`
- Test: `STASIS.Tests/Integration/BoxSearchVisibilityTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.GetVisibleStudyIdsAsync` (Task 3).
- Produces: updated `IStorageService` signatures above (no other method
  needs a filter here; box detail/placement write paths are Task 11-12).

- [ ] **Step 1: Write failing tests**: a Viewer on Study A searching boxes
  never sees a box whose `StudyID` is Study B; an **unassigned empty box**
  (`StudyID == null`) is visible to a user who has access to *at least
  one* study — it's not yet claimed by any study, so hiding it would make
  it undiscoverable to place a first specimen into; a user with **zero**
  study assignments sees no boxes at all, including unassigned ones —
  per the design doc's "zero access means an empty state, never a
  fallback to see everything," which applies here too, not just to
  specimens; a System Admin sees everything.

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Add the filter to each query: when
  `visibleStudyIds` is non-null, first check `visibleStudyIds.Count == 0`
  — if so, the query returns nothing (don't run the box query at all, or
  add a `Where(_ => false)`, implementer's choice). Otherwise filter with
  `box.StudyID == null || visibleStudyIds.Contains(box.StudyID.Value)`.
  When `visibleStudyIds` is null (System Admin), no filter at all. Wire
  `SearchModel.OnGetAsync` to resolve and pass `GetVisibleStudyIdsAsync`
  through to all four service calls it currently makes unfiltered.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

---

## Phase 5 — Write-side enforcement and the box/study fix

### Task 11: `Box.StudyID` validation and assignment in `StorageService`

**Files:**
- Modify: `STASIS/Services/IStorageService.cs`, `STASIS/Services/StorageService.cs`
  (`MoveSpecimenAsync`, `ReboxSpecimensAsync`, `CheckAndUnassignEmptyBoxAsync`)
- Test: `STASIS.Tests/Services/BoxStudyEnforcementTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  Task ValidateAndAssignBoxStudyAsync(int boxId, int studyId);
  ```
  Throws `InvalidOperationException` if the box's current `StudyID` is
  non-null and different from `studyId`. If the box's `StudyID` is null,
  sets it to `studyId` on the tracked entity **without** calling
  `SaveChangesAsync` — the caller's existing save persists it as part of
  the same unit of work. No-op if they already match.
- Consumes: nothing new (uses the service's own `_context`).

- [ ] **Step 1: Write failing tests**:
  `ValidateAndAssignBoxStudyAsync` on an empty box sets `StudyID`; on a box
  already holding Study A specimens, calling it with Study B throws; on a
  box already matching, no-op and no exception.
  `MoveSpecimenAsync` moving a Study B specimen into a box that currently
  holds Study A specimens throws (and the specimen is not moved —
  transaction/no-op on failure) and `ReboxSpecimensAsync` likewise rejects
  a batch containing a mismatched study before any row is written.
  `CheckAndUnassignEmptyBoxAsync` clears `StudyID` to null (in addition to
  the existing `RackID` clearing) once a box's last specimen leaves,
  regardless of `BoxCategory` (the existing `RackID`-clearing condition
  stays scoped to `"Standard"`; the new `StudyID`-clearing condition is
  **not** scoped to category — a Temp/Trash/Shipping box with zero
  specimens has no study affiliation to keep either).

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement** `ValidateAndAssignBoxStudyAsync` as specified
  above. Call it from `MoveSpecimenAsync` and `ReboxSpecimensAsync` before
  mutating `specimen.BoxID`, using the specimen's own `StudyID` (already a
  loaded scalar — no extra query needed for `MoveSpecimenAsync`; for
  `ReboxSpecimensAsync`, validate each specimen's `StudyID` against
  `newBoxId` inside the existing `foreach`, before any field is set, so a
  mismatch anywhere in the batch aborts before partial writes). Let the
  thrown `InvalidOperationException` propagate — callers (Task 13/14)
  catch it. Add the `StudyID = null` clearing line to
  `CheckAndUnassignEmptyBoxAsync` alongside the existing `RackID` logic.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 12: `Samples/Add` — Editor access and box-study validation

**Files:**
- Modify: `STASIS/Services/ISampleService.cs`, `STASIS/Services/SampleService.cs`
  (`AddSpecimensBatch`, constructor gains `IStorageService`)
- Modify: `Pages/Samples/Add.cshtml.cs`
- Test: `STASIS.Tests/Integration/AddSampleAccessTests.cs`

**Interfaces:**
- Consumes: `IStorageService.ValidateAndAssignBoxStudyAsync` (Task 11),
  `IStudyAccessService.CanEditAsync`, `GetVisibleStudyIdsAsync` (Task 3).
- Produces: `SampleService(StasisDbContext, IAuditService, IStorageService)`
  (new constructor parameter — update the DI registration call sites; none
  exist outside `Program.cs`'s `AddScoped` which needs no change).

- [ ] **Step 1: Write failing tests**: posting `Add` with a `StudyID` the
  caller can't edit is rejected server-side even if the dropdown wouldn't
  have offered it; the Study dropdown only lists studies where
  `CanEditAsync` is true; a batch that targets a box already holding a
  different study's specimens fails with a `ModelState` error and **no**
  specimens from the batch are saved (the existing transaction in
  `AddSpecimensBatch` already rolls back on exception — confirm this with
  the test rather than re-implementing rollback).

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** In `AddSpecimensBatch`, before
  `_context.Specimens.Add(specimen)` for each specimen with
  `specimen.BoxID.HasValue`, call
  `await _storageService.ValidateAndAssignBoxStudyAsync(specimen.BoxID.Value, specimen.StudyID);`
  inside the existing `try` block so a thrown `InvalidOperationException`
  is caught by the same `catch`/rollback path already there (extend the
  existing `catch` to also catch `InvalidOperationException` if it
  currently only catches more narrowly — check the existing block). In
  `Add.cshtml.cs`: remove `[Authorize(Roles = "Write,Admin")]`, replace
  with an `OnGetAsync`/`OnPostAsync` check — `Forbid()` if
  `GetVisibleStudyIdsAsync` is empty and not System Admin (no studies to
  add to at all); filter `StudyOptions` to editable studies only; in
  `OnPostAsync`, after `ModelState.IsValid`, re-validate
  `CanEditAsync(userId, StudyID!.Value)` and add a `ModelState` error if
  false (don't trust the posted value). Catch the new
  `InvalidOperationException` from `AddSpecimensBatch` alongside the
  existing `DbUpdateException` catch, surfacing its message as a
  `ModelState` error.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 13: CSV import per-row access check

**Files:**
- Modify: `STASIS/Services/ISampleService.cs`, `STASIS/Services/SampleService.cs`
  (`ImportSpecimensFromCsv` gains an `IReadOnlyCollection<int>? editableStudyIds` parameter)
- Modify: `Pages/Samples/Import.cshtml.cs`
- Test: `STASIS.Tests/Integration/ImportAccessTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.GetVisibleStudyIdsAsync` filtered to
  Editor+ (a new small helper is fine here, or filter in the page — your
  call; keep it off `IStudyAccessService`'s public surface if it's just a
  `Where` over `GetRoleAsync` results, to avoid growing that interface for
  a single caller).
- Produces: a row whose resolved study is outside `editableStudyIds` is
  added to `result.ErrorRows` with
  `importRow.Error = $"You do not have edit access to study '{importRow.StudyCode}'."`
  — rows for editable studies still proceed to `SuccessRows`.

- [ ] **Step 1: Write failing tests**: a file with rows for both an
  editable and a non-editable study imports the editable rows and rejects
  the others individually (not the whole file); a file entirely for
  non-editable studies produces zero `SuccessRows`, all `ErrorRows`.

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** In `ImportSpecimensFromCsv`, after resolving
  `studyId` for a row (existing code), add the access check immediately
  after the existing "study not found" check and before the barcode
  uniqueness check, using the same `continue`-on-error pattern already
  used throughout that loop. In `Import.cshtml.cs`, remove
  `[Authorize(Roles = "Write,Admin")]`, replace with: `Forbid()` if the
  caller has no Editor-or-above access to any study at all; resolve the
  caller's editable-study set once per request and pass it into both
  `OnPostPreviewAsync` and `OnPostCommitAsync`'s calls to
  `ImportSpecimensFromCsv`.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

### Task 14: Remaining Write-gated pages — Discard, Move, Rebox, Create

**Files:**
- Modify: `Pages/Samples/Discard.cshtml.cs`
- Modify: `Pages/Boxes/Move.cshtml.cs`
- Modify: `Pages/Boxes/Rebox.cshtml.cs`
- Modify: `Pages/Boxes/Create.cshtml.cs`
- Test: `STASIS.Tests/Integration/RemainingWriteGatesTests.cs`

**Interfaces:**
- Consumes: `IStudyAccessService.CanEditAsync`, `GetVisibleStudyIdsAsync` (Task 3).

- [ ] **Step 1: Write failing tests**, one per page:
  - `Discard`: `OnPostRequestAsync` rejects (per-specimen, skip rather than
    error the whole request — a mixed batch of editable and
    non-editable-study barcodes discards only the editable ones, same
    per-row philosophy as Task 13) any specimen whose `StudyID` fails
    `CanEditAsync`.
  - `Move`/`Rebox`: `OnPostMoveAsync`/`OnPostCommitAsync` re-validate
    `CanEditAsync` against the specimen's (current) `StudyID` before
    calling into `StorageService`; the box-option dropdowns are filtered
    to boxes that are unassigned or match a study the caller can edit
    (reuse Task 10's filtered `IStorageService` methods instead of the
    unfiltered manual freezer/compartment/rack/box loop these two page
    models currently hand-roll).
  - `Create`: creating a box requires `CanEditAsync` on **at least one**
    study (box creation isn't tied to a specific study yet, so this is an
    "is this person an Editor anywhere" check, not a per-study one).

- [ ] **Step 2: Run tests, confirm they fail**

- [ ] **Step 3: Implement.** Remove `[Authorize(Roles = "Write,Admin")]`
  from all four page models. Add the specific check described per page
  above, using `Forbid()` for the whole-page gate (`Create`) and per-item
  skip/`ModelState` errors for the batch operations (`Discard`, `Move`,
  `Rebox`), consistent with how `Add` (Task 12) and `Import` (Task 13)
  handle it. Replace `Move`/`Rebox`'s manual `LoadBoxOptionsAsync` nested
  loops with a single call to the filtered `IStorageService.GetAllBoxesAsync(visibleStudyIds)`
  from Task 10.

- [ ] **Step 4: Run tests, confirm they pass**

- [ ] **Step 5: Commit**

---

## Phase 6 — Cross-cutting verification

### Task 15: Security/IDOR integration pass and the SecurityStampValidator fix

**Files:**
- Create: `STASIS.Tests/Security/StudyAccessIdorTests.cs`
- Modify: `Program.cs` (tighten `SecurityStampValidatorOptions.ValidationInterval`)

**Interfaces:**
- Consumes: `STASIS.Tests.TestWebApplicationFactory` (created in Task 4 —
  reuse it via `IClassFixture<TestWebApplicationFactory>`; do not create a
  second fixture), and the full stack built in Tasks 1-14.

- [ ] **Step 1: Write failing tests**, each using the shared
  `TestWebApplicationFactory` from Task 4 against its test Postgres
  database (migrate/reset-between-tests behavior already built there):
  - A Viewer-only user posting `Samples/Add` with a tampered `StudyID` for
    a study they don't have Editor access to gets a `ModelState` error,
    and no specimen is created.
  - A user with zero study assignments hitting `/Samples` sees an empty
    result set and the Study dropdown is empty, not "all studies."
  - A user revoked mid-session (their `UserStudyAccess` row deleted via
    `EditUser`'s revoke handler from Task 7) loses access on their very
    next request — not just after a fresh login. This is the test that
    exercises the `SecurityStampValidator` finding; it will fail until
    Step 3 below.
  - A System Admin accessing a study they have no personal
    `UserStudyAccess` row for succeeds, and produces an `IAuditService`
    entry tagged distinctly (per the design doc's "SystemAdminOverride"
    convention) — confirm the entry exists and is attributable to that
    user and that study.

- [ ] **Step 2: Run tests, confirm the revocation-timing one fails**

- [ ] **Step 3: Tighten the Identity cookie validation interval** in
  `Program.cs`'s `AddDefaultIdentity` configuration — add
  `options.SecurityStampValidationInterval = TimeSpan.FromSeconds(0);`
  if that option is exposed directly on `IdentityOptions` in this ASP.NET
  Core version, otherwise configure
  `services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);`
  explicitly. Confirm which API surface this .NET 10 Identity version
  actually exposes before committing to one — it's moved around across
  ASP.NET Core versions.

- [ ] **Step 4: Run all tests in this task, confirm they pass**

- [ ] **Step 5: Commit**

### Task 16: `StasisDataMigration` — scope `get_or_create_box` by study

**Files:**
- Modify: `../StasisDataMigration/migration/load_target.py:174-224`
  (`get_or_create_box`, and its one caller at line 224)
- Test: `../StasisDataMigration/tests/test_load_target_box_scoping.py`

**Interfaces:**
- Consumes: nothing from the STASIS repo — this is a standalone fix in the
  sibling `StasisDataMigration` project, run and committed there, not as
  part of the STASIS repo's git history.
- Produces: `get_or_create_box(cur, box_label: str, box_type: str, rack_id: int | None, study_id: int) -> int`
  (adds the `study_id` parameter).

- [ ] **Step 1: Write a failing test** in that project's existing
  `pytest`-based suite (see `tests/test_transform.py` for the existing
  pattern/fixtures used there): calling `get_or_create_box` twice with the
  same `box_label` but two different `study_id` values raises instead of
  silently reusing the first box's row.

- [ ] **Step 2: Run test, confirm it fails**

- [ ] **Step 3: Implement.** Add `study_id` to the function signature and
  to its one call site (line 224). Change the `INSERT ... ON CONFLICT ("BoxLabel") DO UPDATE`
  to also check the existing row's `StudyID`: if the box already exists
  with a different non-null `StudyID`, raise a clear `ValueError` naming
  the conflicting box label and both study IDs (this surfaces as a load
  failure the data manager has to resolve by hand, which matches this
  project's existing philosophy of blocking on data problems rather than
  guessing — see the README's "Location rule" section for the same pattern
  applied to missing freezer hierarchy). If the existing row's `StudyID`
  is null, update it to `study_id` (first specimen claims the box, same
  rule as the C# side in Task 11).

- [ ] **Step 4: Run test, confirm it passes**

- [ ] **Step 5: Before committing, re-run validation against whatever is
  currently in the live target database** (not just the 2026-07-20 dump
  checked during design) — run
  `python3 -m migration.load_target --source-id <each pending source> --dry-run`
  for any source not yet loaded, and separately query the live
  `tbl_Boxes`/`tbl_Specimens` tables directly for any box already spanning
  more than one `StudyID` before this fix ships. If any are found, that's
  a data-manager cleanup task, not something to silently resolve in code.

- [ ] **Step 6: Commit** (in the `StasisDataMigration` repo, separately
  from this plan's STASIS commits)

---

## Self-Review Notes

- **Spec coverage:** every Goal in the design doc maps to a task — role
  tiers (Tasks 3-8), per-study Viewer/Editor (Tasks 9-14), zero-access
  empty state (Task 9/10), prompt revocation (Task 15), last-Study-Admin
  guardrail and confirmation prompts (Task 7), box/study fix (Tasks 2, 11,
  16), server-side re-validation on every write (Tasks 12-14), System
  Admin override logging (Tasks 8, 15).
- **Proportion check:** this plan is longer than the design doc because it
  touches ~20 existing files individually; each task's step list stays
  short (test → fail → implement → pass → commit) and defers method bodies
  to signatures/assertions except where an algorithm (the box-study
  precedence logic, the per-row CSV rejection) isn't otherwise determined.
- **Known open call for the implementer:** Task 15 Step 3 names two
  possible Identity API shapes for the validation-interval setting because
  this specific .NET 10 Identity package's exact option surface wasn't
  verified against documentation during planning — resolve by checking
  the installed `Microsoft.AspNetCore.Identity` version's actual type at
  implementation time.

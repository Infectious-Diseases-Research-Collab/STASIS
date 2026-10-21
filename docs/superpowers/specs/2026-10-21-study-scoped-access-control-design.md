# Study-Scoped Access Control for STASIS

**Date:** 2026-10-21
**Status:** Draft — pending approval

## Intent and why

STASIS currently has no study-level access control at all. Any authenticated
user — regardless of their global role (Admin, Write, or Read) — can see and
act on every study's specimens, boxes, and freezers. This is being done
proactively, before onboarding more studies and staff and before a product
launch, not in response to an incident.

The two real studies named in conversation are "Critical" and "Prism" —
different people manage and work on each, sometimes the same person works on
both, and access needs to be grantable per study, independently.

## Goals

- A user can be granted access to one study, several studies, or none.
  Access is never granted by default — only by deliberate assignment.
- Within a study a user's role determines what they can do: **Viewer**
  (read-only) or **Editor** (can add/edit/move/discard specimens in that
  study). The same person can be Editor on one study and Viewer on another.
- Each study can have one or more **Study Admins** — staff who manage who
  has access to *that study only*: add/remove staff, set Viewer/Editor,
  create brand-new user accounts for their study, and promote another staff
  member on the same study to Study Admin. A Study Admin has no rights over
  any study they aren't assigned to.
- A **System Admin** (global) can create studies, assign the first Study
  Admin to a new study, manage any user account, and — for support or
  compliance reasons — act on any study's data regardless of assignment.
  This bypass is explicit in code and logged distinctly from normal
  study-scoped access whenever a System Admin touches a study they are not
  personally assigned to.
- A user can exist with zero study access and will see an explicit empty
  state, never a fallback to "see everything."
- Revoking or changing access takes effect promptly — within one request,
  not only after the user's next login. (ASP.NET Core Identity's
  `SecurityStampValidator` interval needs to be checked/tightened for this;
  flagged as a known risk, see Findings.)
- Enforcement is server-side on every study-scoped read and write.
  Dropdowns and nav links may be filtered for convenience, but hiding
  something in the UI is never the actual security boundary.
- Removing the last Study Admin from a study is blocked — a study can never
  be left with zero admins through the UI.
- Revoking a user's access to a study, or demoting/removing a Study Admin,
  requires a confirmation step — no one-click mass revocation.
- A box cannot hold specimens from more than one study. This is a
  **confirmed bug** found during this review (nothing in `StorageService` or
  `SampleService` enforces it today) and fixing it is part of this work,
  because it's what makes study-scoped filtering of boxes/freezers simple
  (filter by box, not by individual slot).

## Non-goals

- Deny rules / explicit blocklists. Access is purely additive — confirmed
  directly: "no one gets study access unless someone deliberately adds
  them." There is no scenario requiring an active block.
- Any permission granularity finer than Viewer / Editor / Study Admin
  within a study.
- Background jobs, caches, external APIs, bulk exports, or nested
  comment/attachment systems — none of these exist in STASIS today. The
  original audit brief that prompted this covered them generically; they're
  explicitly out of scope here per direct confirmation.
- Rebuilding Shipments functionality (still placeholder pages). This design
  notes where the access check will need to plug in once that's built, but
  doesn't build it now.
- Migrating legacy *user* access assignments. Only one real user exists
  today (the seeded `admin@stasis.com` account) — there is no user-access
  assignment data to migrate. The fake-data script
  (`Database/02_STASIS_insert_fake_data.sql`) creates studies/specimens but
  no additional `AspNetUsers` rows.
  (Specimen/box data is a different story — see Findings below; it is not
  a non-goal, it's a real constraint on the `Box.StudyID` migration.)

## Anti-goals (what would make this a failure even if it technically works)

- A user ever seeing or modifying data for a study they aren't assigned to —
  via UI, direct URL, a tampered form field (`StudyID`, `BoxID`,
  `SpecimenID`), or a bulk CSV import that references a study the uploader
  doesn't have Editor access to.
- A new generic, over-abstracted permissions framework when a single join
  table with a role column does the job.
- Breaking the existing System Admin account or locking out the only real
  user during rollout.
- Retiring the global Write/Read roles in a way that leaves any
  currently-working page without an access check at all (fail open).

## Constraints

- .NET 10 Razor Pages, PostgreSQL via EF Core, ASP.NET Core Identity
  (`IdentityUser`, `IdentityRole`) already in place.
- The three global Identity roles (Admin, Write, Read) are seeded at
  startup in `Program.cs`. Write and Read are retired by this work; Admin is
  kept and its meaning narrows to "System Admin."
- EF Core migrations are the real source of truth for schema today (despite
  a note in `CLAUDE.md` suggesting otherwise) — three migrations already
  exist (`InitialCreate`, `AddVisitTypes`, `BarcodeStudyCompositeKey`). New
  schema changes go through `dotnet ef migrations add`.
- Page models must never call `DbContext` directly — they go through
  services (`ISampleService`, `IStorageService`, etc.), per existing
  convention.
- `AuditLog` + `AuditService` already exist and are actively used for
  user/study changes (`CreateUser`, `EditUser`, `LabSetup/Studies`). Reuse
  them for access grant/revoke/promotion logging and for System Admin
  override logging — don't build a second audit mechanism.
- Table naming convention is `tbl_X`.
- Physical storage (Freezer → Compartment → Rack → Box → Specimen) is not
  inherently study-scoped today; `Box` has no `StudyID` column.

## Decided design

### Roles — three tiers

1. **System Admin** — global. Maps to the existing Identity `"Admin"` role.
   Creates/edits/deletes studies, creates any user account, grants/revokes
   System Admin on other users, designates the first Study Admin on any
   study, and can view/act on any study regardless of assignment. Every
   action taken on a study the System Admin isn't personally assigned to is
   logged via `AuditService` with a distinct action tag (e.g.
   `"SystemAdminOverride"`) so it's traceable later.

2. **Study Admin** — scoped to specific studies via the same assignment
   table (role = `StudyAdmin`). Within their study/studies: creates new
   user accounts and assigns them to the study, adds/removes any staff
   member's access, sets a staff member's role (Viewer/Editor), and
   promotes/demotes other Study Admins on that same study. Cannot create
   studies. Cannot touch a study they aren't assigned to. Described in
   conversation as "the final boss of that study," expected to handle most
   day-to-day user administration so the System Admin doesn't have to.

3. **Staff** — assigned Viewer or Editor on zero or more studies
   independently. Viewer: read-only within assigned studies. Editor: can
   add/edit/move/discard specimens within assigned studies. No access
   outside assigned studies. Zero studies assigned → empty state.

The global Identity roles `"Write"` and `"Read"` are retired — what a user
can *do* is now per-study (Viewer/Editor), not global. `"Admin"` is kept,
now exclusively meaning System Admin.

### Data model

New table `tbl_UserStudyAccess`:

| Column | Notes |
|---|---|
| `UserStudyAccessID` | PK |
| `AspNetUserId` | FK → `AspNetUsers`. `OnDelete(Restrict)` — deleting a user account is a deliberate deactivation decision, not something that should silently cascade-delete access history. |
| `StudyID` | FK → `tbl_Studies`. `OnDelete(Cascade)` — a study can only be deleted today when it has zero specimens (existing check in `StudiesModel.OnPostDeleteAsync`), so cascading its access rows is safe. |
| `Role` | `text`, check constraint `IN ('Viewer', 'Editor', 'StudyAdmin')`. |
| `GrantedByUserId` | who granted this row. |
| `GrantedDate` | default `CURRENT_TIMESTAMP`. |

- Unique index on `(AspNetUserId, StudyID)` — one role per user per study.
- Index on `StudyID` (who has access to this study) and `AspNetUserId`
  (which studies can this user see).

`tbl_Boxes` gains a nullable `StudyID` (FK → `tbl_Studies`,
`OnDelete(Restrict)`). It is set when the first specimen is placed into an
empty box, validated to match on every subsequent placement/move into that
box (reject if it doesn't), and cleared when the box becomes empty (hook
into the existing box-emptying logic in `StorageService`). This is what
turns "can this user see this box" into a simple indexed filter instead of
per-slot masking, and it's also the fix for the box/study-mixing bug found
during this review and for deferred item 9 in `STASIS_Deferred_Items.md`.

### Authorization enforcement

A new `IStudyAccessService`, injected into page models and the existing
services wherever a study-scoped decision is needed:

- `IsSystemAdmin(ClaimsPrincipal user)`
- `GetAccessibleStudyIds(string userId)` → the allow-list (or "all" for
  System Admin)
- `GetRole(string userId, int studyId)` → `None` / `Viewer` / `Editor` /
  `StudyAdmin` (`StudyAdmin` implies Editor-level content rights)
- `CanEdit(string userId, int studyId)`, `CanManageStudy(string userId, int studyId)`

Every study-scoped read filters by `GetAccessibleStudyIds`:
`SampleService.GetSpecimensAsync`, `GetSpecimenByIdAsync`/`Detail`,
`StorageService` box search/detail, the Administration user listing (Study
Admins see only users with access to their studies; System Admin sees
everyone), and the Study/Visit Type/Box dropdowns across the Samples pages.

Every write re-validates server-side that the resolved `StudyID` — from
form data, query string, or a `SpecimenID`/`BoxID` that must first be
resolved to its study — is in the caller's accessible set with sufficient
role. The UI only *offering* valid choices is never sufficient on its own.

This specifically closes a confirmed gap found in `Samples/Import.cshtml.cs`:
each CSV row resolves its own `StudyCode` independently, so a single upload
can span multiple studies. Each row's resolved study must be checked
against the uploader's accessible-and-Editor studies; rows for studies the
uploader can't edit are rejected, not silently imported.

### Frontend

Study Admins get a scoped slice of the Administration area: the user list
is filtered to people who have access to their study/studies (System Admin
still sees everyone), and they can create accounts and manage per-study
roles for their own studies from there. Exact page/route layout (reuse
`Administration/Users` + `CreateUser` with scoping vs. new dedicated pages)
is left to the builder.

## Findings from this review (for awareness, not all are in scope to fix here)

- **Confirmed, in scope for this work:** no study-level access control
  exists anywhere; any authenticated user sees/can edit all studies'
  specimens, boxes, and freezers today.
- **Confirmed, in scope for this work:** nothing prevents a box from holding
  specimens from more than one study.
- **Confirmed, in scope for this work — real data, not just the fake-data
  script:** a separate project, `StasisDataMigration` (sibling folder to
  this repo), actively loads real legacy specimen data from multiple
  sources (Prism, Prism2, MICD, MMV, Sonet1, start_up, Immerse, and more)
  into the live STASIS target tables. The most recent load dump
  (`reports/load/stasis_clean_sources_target_data_2026-07-20.sql`, ~270k
  specimen rows across ~4,100 boxes) was checked directly and currently has
  **zero** boxes spanning more than one study. But `migration/load_target.py
  get_or_create_box()` resolves/creates boxes **globally by `BoxLabel` only**
  (`ON CONFLICT ("BoxLabel") DO UPDATE`), with no awareness of study —
  it's clean today by chance (no two sources have collided on a label yet),
  not by design. Several more sources are still queued for review (per the
  `reports/*_current_review/` folders), so a future load could still create
  a cross-study box before this fix ships. Implementation needs to: (a)
  re-validate against whatever is actually in the live target DB at
  migration time, not just this static dump, and (b) fix
  `get_or_create_box()` in the migration tool to scope box resolution by
  study too, so it can't violate the new constraint on later loads.
- **Confirmed, in scope for this work:** `Samples/Import.cshtml.cs` resolves
  study per CSV row with no check against the uploader's access — a bulk
  cross-study IDOR risk once access control exists.
- **Confirmed, needs verification during implementation, not fully
  resolved here:** ASP.NET Core Identity's cookie auth doesn't re-check role
  claims/security stamp on every request by default — there's a
  `SecurityStampValidator` interval (commonly defaulting to 30 minutes).
  "Access changes take effect promptly" (a goal above) depends on this
  being tightened; needs to be checked against the actual configured value
  and adjusted if needed.
- **Informational:** the nav bar (`Pages/Shared/_Layout.cshtml`) shows every
  link (Administration, Lab Setup, etc.) to every user regardless of role —
  not a security hole (server-side `[Authorize]` attributes already gate
  the underlying pages), but a rough UX edge once Viewer-only staff start
  clicking into things they can't use.

## Decisions settled during builder-check (my call — flag if any look wrong)

1. **System Admin override logging covers writes only, not reads.** Logging
   every list/search page view a System Admin makes (which touches "all"
   studies by design, since that's the point of the bypass) would be noisy
   with little value. Logging triggers on write actions — create/edit/move/
   discard a specimen or box, grant/revoke/promote study access — performed
   on a study the System Admin isn't personally assigned to.
2. **"Can't remove the last Study Admin" is absolute, including for System
   Admin.** Adding a Study Admin is never blocked (that's how a brand-new
   study gets its first one). Removing/demoting down to zero is blocked for
   everyone; to replace a Study Admin, add the new one first, then remove
   the old one.
3. **System Admin is not auto-assigned to a study they create.** Creating a
   study or designating its first Study Admin does not insert a
   `tbl_UserStudyAccess` row for the System Admin themselves. If they want
   ongoing assigned access (e.g. to appear in that study's normal member
   list), they add themselves explicitly, same as anyone else.
4. **CSV import rejects per-row, not the whole file, on access failures.**
   Consistent with how `ImportSpecimensFromCsv` already handles other
   validation failures (bad study code, duplicate barcode, etc.) —
   "uploader lacks Editor access to this row's study" becomes another
   per-row entry in `ErrorRows`; valid rows for studies the uploader can
   edit still import.
5. **Write/Read Identity roles are stopped-using, not deleted.**
   `Program.cs` stops seeding/assigning them, and the four
   `[Authorize(Roles = "Write,Admin")]` attributes (`Samples/Add`,
   `Samples/Import`, `Boxes/Create`, `Shipments/Create`) are replaced with
   study-scoped checks. The now-dormant `AspNetRoles` rows aren't
   force-deleted — removing them later is a trivial optional cleanup, not
   required for correctness.

## What's left to the builder

- Exact page/route layout for the Study Admin's scoped user-management UI
  (reuse `Administration/Users`/`CreateUser` with scoping vs. new pages).
- Exact wording of empty states, confirmation dialogs, and other minor UX
  text.
- Whether "deactivate a user" is a soft-delete flag or Identity's lockout
  mechanism — pick whichever fits the existing Identity setup with the
  least new surface area.
- Naming/shape of any `IStudyAccessService` helper methods beyond what's
  specified above.
- How exactly the `SecurityStampValidator` interval finding gets resolved
  (config change vs. explicit sign-out-on-revoke call) — investigate during
  implementation and pick the simplest fix.

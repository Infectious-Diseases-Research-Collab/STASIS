# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

STASIS (Specimen Tracking And Storage Information System) is an ASP.NET Core Razor Pages application targeting `.NET 10` with PostgreSQL 17 as the database backend. It manages the lifecycle of biological specimens — intake, storage, movement, shipment, and discard — for a biorepository facility.

## Commands

Run all commands from the repository root.

```bash
dotnet restore                          # Restore NuGet packages
dotnet build STASIS.sln                 # Compile and catch errors
dotnet run --project STASIS.csproj      # Run the app
dotnet watch run --project STASIS.csproj  # Run with hot reload
dotnet user-secrets list                # Verify secrets are set
```

EF Core migrations (once adopted as source of truth):
```bash
dotnet ef migrations add <Name> --project STASIS.csproj
dotnet ef database update --project STASIS.csproj
```

## Local Setup

1. Create the PostgreSQL role and database:
   ```bash
   psql postgres -c "CREATE ROLE stasis_app WITH LOGIN PASSWORD 'YOUR_PASSWORD';"
   createdb -O stasis_app stasis
   ```
2. Apply the bootstrap script (until EF migrations replace it):
   ```bash
   psql -U stasis_app -d stasis -f STASIS/STASIS_create_tables_postgres.sql
   ```
3. Store the connection string via .NET User Secrets (never put passwords in `appsettings.Development.json`):
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "Host=localhost;Port=5432;Database=stasis;Username=stasis_app;Password=YOUR_PASSWORD"
   ```
4. The seeded admin account is `admin@stasis.com` / `Admin123!` (development only).

## Architecture

```
/Program.cs                 — App entry point, DI registration, admin seed
/STASIS.csproj              — .NET 10, Npgsql EF provider, Identity packages
/appsettings.json           — Base config (no secrets)

/STASIS/
  Data/StasisDbContext.cs   — EF Core DbContext (extends IdentityDbContext)
  Models/                   — Domain entities (Specimen, Box, Freezer, Rack,
                              Shipment, ShipmentBatch, ShipmentRequest,
                              ShipmentContent, Approval, FilterPaperUsage,
                              AuditLog, Study, SampleType, UserProfile)
  Services/                 — ISampleService/SampleService, IStorageService/StorageService

/Services/                  — Shared infrastructure: EmailSender, PasswordChangeFilter

/Pages/
  Samples.cshtml(.cs)       — Sample search (paging, barcode/study/type filters) — WORKING
  Samples/Add*              — Placeholder
  Samples/Import*           — Placeholder
  Boxes/                    — Search, Move, Rebox — all placeholders
  Shipments/                — All placeholders
  LabSetup/                 — All placeholders
  Administration/           — Users, CreateUser, EditUser — WORKING; Audit — placeholder

/Areas/Identity/            — Scaffolded ASP.NET Core Identity UI
/wwwroot/                   — Static assets (CSS, JS, images)
```

### Key Patterns

- **Authentication/Authorization:** All routes require authentication via a fallback policy. `PasswordChangeFilter` forces a password change on first login. Roles: `Read`, `Write`, `Admin`.
- **Data access:** Services (`ISampleService`, `IStorageService`) are injected into page models; never call `DbContext` directly from page models.
- **Naming:** Tables use `tbl_` prefix (e.g., `tbl_Specimens`). EF entities are mapped explicitly in `OnModelCreating`. Check constraints and unique indexes are defined there.
- **Storage hierarchy:** `Freezer → Compartment → Rack → Box → Specimen (PositionRow/PositionCol)`. A unique index on `(BoxID, PositionRow, PositionCol)` enforces conflict detection at the DB level. Compartments have a unique index on `(FreezerID, CompartmentName)`.
- **Special sample rules:** Filter Paper specimens track `RemainingSpots`, `SpotsShippedInternational`, `SpotsReservedLocal`. Plasma specimens use `AliquotNumber` (1 or 2). These rules are domain-critical but mostly unimplemented in services.

## Implementation Status

See `STASIS_Implementation_Plan.md` for the full phase-by-phase roadmap. Key known gaps:
- EF Core migrations not yet adopted; schema and models may drift — reconcile before new feature work.
- Many page handlers are empty placeholders (`Add`, `Import`, `Boxes/*`, `Shipments/*`, `LabSetup/*`).
- Audit log table and model exist, but no change logging is written anywhere.
- Approval workflow entities exist in the model but have no UI or service logic.

## Coding Conventions

- 4-space indentation, standard C# brace style.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`).
- Public types and page models: PascalCase. Locals and parameters: camelCase. Interfaces prefixed with `I`.
- Group feature files by folder: `Pages/Boxes/*`, `Pages/Shipments/*`, etc.
- Commit messages: short, sentence-style, past tense (e.g., `Added sample import workflow`).

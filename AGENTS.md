# Repository Guidelines

## Project Structure & Module Organization
`STASIS` is an ASP.NET Core Razor Pages application targeting modern .NET. UI pages live in `Pages/` with paired page models such as `Pages/Samples/Search.cshtml` and `Pages/Samples/Search.cshtml.cs`. Identity UI is under `Areas/Identity/`. Domain models, EF Core data access, and business services are in `STASIS/Models/`, `STASIS/Data/`, and `STASIS/Services/`. Shared infrastructure services such as email and password filters live in `Services/`. Static assets are in `wwwroot/`. Database bootstrap scripts live in `STASIS/STASIS_create_tables_postgres.sql` and `STASIS/update_user_profiles.sql`; keep them aligned with the active PostgreSQL schema or replace them with EF Core migrations.

## Build, Test, and Development Commands
Run commands from the repository root.

- `dotnet restore`: restore NuGet packages for `STASIS.sln`.
- `dotnet build STASIS.sln`: compile the Razor Pages app and catch analyzer/compiler errors.
- `dotnet run --project STASIS.csproj`: start the app locally.
- `dotnet watch run --project STASIS.csproj`: run with hot reload during page-model or Razor edits.

Before first run, create a local PostgreSQL database named `stasis`, apply the PostgreSQL-compatible bootstrap script or EF Core migrations, and set `ConnectionStrings:DefaultConnection` with .NET User Secrets or another secret store.

## Coding Style & Naming Conventions
Use 4-space indentation and standard C# brace style as shown in `Program.cs`. Keep nullable reference types enabled and prefer constructor or DI-based service access over static helpers. Use PascalCase for public types and page models, camelCase for locals and parameters, and keep interfaces prefixed with `I` such as `ISampleService`. Match Razor page names to their page-model class names and keep feature files grouped by folder (`Pages/Boxes/*`, `Pages/Shipments/*`).

## Testing Guidelines
There is currently no dedicated test project in the repository. At minimum, run `dotnet build` before opening a PR and manually verify the affected Razor Pages against a local PostgreSQL database. If you add automated tests, place them in a separate `*.Tests` project at the solution root and use descriptive names like `SampleServiceTests` and `Search_returns_expected_results`.

## Commit & Pull Request Guidelines
Recent commits use short, sentence-style summaries in past tense, for example `Added user authentication and management` and `Updated README`. Follow that pattern and keep each commit focused on one change. Pull requests should include a concise description, database or config changes, linked issues if applicable, and screenshots for Razor UI updates.

## Security & Configuration Tips
Do not commit real connection strings, mail credentials, or seeded production accounts. Keep environment-specific values in .NET User Secrets, environment variables, or another secret store, and document any new required settings in `README.md`.

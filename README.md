# STASIS

This project is the Specimen Tracking And Storage Information System (STASIS).

## Local Setup with PostgreSQL

These steps are OS agnostic. You can develop on macOS with VS Code or on Windows with Visual Studio or VS Code. The shared requirement is a supported .NET SDK and a local PostgreSQL instance.

### 1. Install prerequisites

Install the following:

- .NET SDK from `https://dotnet.microsoft.com/download`
- PostgreSQL 17 from `https://www.postgresql.org/download/`
- An editor or IDE such as Visual Studio, VS Code, or JetBrains Rider

Verify the installs from a terminal:

```bash
dotnet --info
psql --version
```

### 2. Create the PostgreSQL database and apply migrations

Create the application login if you have not already done so:

```bash
psql postgres
CREATE ROLE stasis_app WITH LOGIN PASSWORD 'YOUR_POSTGRES_PASSWORD';
\q
```

Create the local database:

```bash
createdb -O stasis_app stasis
```

Confirm that it exists:

```bash
psql -lqt | grep stasis
```

Apply the EF Core migrations to create the schema and seed data. Run from the repository root after setting user secrets (step 3):

```bash
dotnet ef database update --project STASIS.csproj
```

**Existing database (created by the bootstrap script):** If you already applied `STASIS_create_tables_postgres.sql`, mark the initial migration as already applied without re-running it:

```bash
psql -U stasis_app -d stasis -c "
  CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (
    \"MigrationId\" varchar(150) NOT NULL,
    \"ProductVersion\" varchar(32) NOT NULL,
    CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\")
  );
  INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")
  VALUES ('20260303031801_InitialCreate', '10.0.1')
  ON CONFLICT DO NOTHING;
"
```

The bootstrap script (`STASIS/STASIS_create_tables_postgres.sql`) can still be used to wipe and recreate the schema with seed data during early development. For all schema changes going forward, create a new EF Core migration instead of editing the SQL script.

### 3. Configure the application

Store secrets in .NET User Secrets instead of `appsettings.Development.json`. Run both of the following from the repository root:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=stasis;Username=stasis_app;Password=YOUR_POSTGRES_PASSWORD"
dotnet user-secrets set "AdminSeedPassword" "YOUR_ADMIN_PASSWORD"
```

`AdminSeedPassword` is the password assigned to the seeded `admin@stasis.com` account on first startup. The app logs a warning and skips seeding if this value is not set. Choose a strong password and store it somewhere safe — you cannot recover it from the app later.

Verify the stored values if needed:

```bash
dotnet user-secrets list
```

Keep `appsettings.Development.json` free of passwords and other secrets. The application is configured to use `UseNpgsql(...)` with the `Npgsql.EntityFrameworkCore.PostgreSQL` provider.

### 4. Open the project

Open the repository in your preferred tool:

- Visual Studio: open `STASIS.sln`
- VS Code: install `ms-dotnettools.csdevkit`, then run `code .`

If you use VS Code, the C# Dev Kit walkthrough can detect the installed .NET SDK and configure launch settings.

### 5. Build and run the app

From the repository root:

```bash
dotnet restore
dotnet build STASIS.sln
dotnet watch run --project STASIS.csproj
```

If `net10.0` does not build on your machine, update `STASIS.csproj` to a supported target framework such as `net9.0`.

## 6. Developer Smoke Test Checklist

After completing local setup, verify the following before starting feature work:

**Build and startup**
- [ ] `dotnet build STASIS.sln` completes with 0 errors
- [ ] `dotnet run --project STASIS.csproj` starts without exceptions in the console
- [ ] The app is reachable at `https://localhost:5001` (or the port shown in terminal output)

**Database connectivity**
- [ ] The login page loads (confirms the app connected to PostgreSQL successfully)
- [ ] `dotnet ef database update --project STASIS.csproj` reports "No pending migrations" (confirms schema is current)

**Authentication**
- [ ] Logging in with `admin@stasis.com` and the password set in `AdminSeedPassword` succeeds
- [ ] Attempting to access any page while logged out redirects to the login page
- [ ] Logging out returns to the login page

**Core functionality**
- [ ] The Samples page (`/Samples`) loads and displays the seeded specimens
- [ ] The barcode search filter on the Samples page returns results (try `PLASMA-001`)
- [ ] The Study and Sample Type dropdowns on the Samples page are populated
- [ ] The Administration → Users page loads and shows at least the admin account

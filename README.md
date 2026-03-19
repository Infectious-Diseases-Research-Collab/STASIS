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

Follow these steps to initialize the local development database. All SQL scripts are in the `Database/` directory, so `cd Database` first (or prefix each file path with `Database/`).

#### Quick Setup (macOS)

On macOS the default PostgreSQL superuser is typically your OS login. The automation script uses `$(whoami)` for this:

1. **Make the script executable**:
   ```bash
   chmod +x Database/setup_db.sh
   ```
2. **Run the script** from the repository root:
   ```bash
   ./Database/setup_db.sh
   ```

#### Manual Setup (Windows CMD / PowerShell / macOS)

On Windows the default PostgreSQL superuser is `postgres`. On macOS you can substitute your OS username or `postgres` if you configured it that way.

1. **Create the Role**:
   ```bash
   psql -U postgres -f Database/00_STASIS_create_db_user.sql
   ```

2. **Create the Database**:
   ```bash
   psql -U postgres -c "CREATE DATABASE stasis OWNER stasis_app;"
   ```
   *If the database already exists you can ignore the error.*

3. **Initialize Schema**:
   ```bash
   psql -U stasis_app -d stasis -f Database/01_STASIS_create_tables_postgres.sql
   ```

#### Configuration Details
* **Default Superuser**: `postgres` (Windows) or your OS login (macOS)
* **Application User**: `stasis_app`
* **Database Name**: `stasis`
* **Schema**: `public`

#### Apply EF Core migrations

Run from the repository root after setting user secrets (step 3):

```bash
dotnet ef database update --project STASIS.csproj
```

**Existing database (created by the bootstrap script):** If you already applied the SQL scripts above, mark the initial migration as already applied so EF doesn't try to re-create the tables.

On **macOS / Linux / Git Bash**:

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

On **Windows CMD or PowerShell**, save the SQL to a file and run it:

```powershell
psql -U stasis_app -d stasis -f Database/mark_initial_migration.sql
```

This file is already included in the repository at `Database/mark_initial_migration.sql`.


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

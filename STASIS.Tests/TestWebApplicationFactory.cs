using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STASIS.Data;

namespace STASIS.Tests;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> fixture for database-backed
/// integration tests. This is the one shared fixture for the whole study-scoped-access-control
/// plan — later tasks (9, 10, 12, 13, 14, 15) consume this file via
/// <c>IClassFixture&lt;TestWebApplicationFactory&gt;</c> rather than creating their own
/// <c>WebApplicationFactory</c>.
///
/// REQUIREMENT: the environment variable <c>STASIS_TEST_CONNECTION</c> MUST be set before
/// running any test that uses this fixture. It must point at a dedicated, throwaway Postgres
/// database reserved for automated tests — e.g.:
/// <code>
/// export STASIS_TEST_CONNECTION="Host=localhost;Port=5432;Database=stasis_test;Username=stasis_app;Password=YOUR_PASSWORD"
/// </code>
/// This must NEVER be pointed at the real development database ("stasis"): this fixture runs
/// EF Core migrations and <see cref="ResetDatabaseAsync"/> truncates every table, which would
/// destroy real data.
///
/// Lifecycle notes:
/// - Migration is caller-driven, NOT automatic. Simply accessing
///   <see cref="WebApplicationFactory{TEntryPoint}.Services"/> (e.g. via <c>CreateClient()</c> or
///   <c>Services.CreateScope()</c>) does NOT run any EF Core migrations — it only builds and starts
///   the test host, which runs Program.cs's startup seed block against whatever schema already
///   exists. Migrations are applied only when a caller explicitly calls
///   <see cref="MigrateDatabaseAsync"/> or <see cref="ResetDatabaseAsync"/> (the latter migrates
///   as a first step before truncating). Every test class that uses this fixture MUST call one of
///   those two methods itself before touching <c>Services</c> for the first time — typically from
///   its own test class's <c>IAsyncLifetime.InitializeAsync</c> — otherwise it may run against a
///   stale or entirely unmigrated schema.
/// - This factory does NOT reset data automatically between tests. xUnit's
///   <c>IClassFixture&lt;TestWebApplicationFactory&gt;</c> creates exactly one instance per test
///   class, shared by every test method in that class. Each test should call
///   <see cref="ResetDatabaseAsync"/> itself (e.g. at the start of its constructor, or from
///   <c>IAsyncLifetime.InitializeAsync</c>) to get a clean, predictable starting point before
///   seeding its own data. This mirrors how later tasks' test suites are expected to use it.
/// - Parallelization: every test class backed by this fixture shares one physical
///   <c>stasis_test</c> database, and <see cref="ResetDatabaseAsync"/> does a full-schema
///   <c>TRUNCATE ... CASCADE</c>. xUnit v2 runs distinct test classes in parallel by default,
///   which would let one class's reset wipe another class's in-flight data. This assembly
///   disables that via <c>[assembly: CollectionBehavior(DisableTestParallelization = true)]</c>
///   in <c>AssemblyInfo.cs</c> — do not remove that attribute or re-enable parallelization while
///   any test class uses this fixture.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The connection string for the throwaway test database, read once from
    /// <c>STASIS_TEST_CONNECTION</c>.
    /// </summary>
    public static readonly string ConnectionString = ResolveConnectionString();

    private static string ResolveConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("STASIS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The STASIS_TEST_CONNECTION environment variable must be set to a connection " +
                "string for a dedicated, throwaway test database before running database-backed " +
                "integration tests. It must NOT point at the real 'stasis' development database. " +
                "Example: export STASIS_TEST_CONNECTION=\"Host=localhost;Port=5432;" +
                "Database=stasis_test;Username=stasis_app;Password=YOUR_PASSWORD\"");
        }

        return connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,

                // Program.cs only runs its admin/role seed block when AdminSeedPassword is
                // configured. Giving it a value here means the test host exercises that seed
                // block the same way the real app does in an environment where it's configured,
                // so tests can assert on its behavior.
                ["AdminSeedPassword"] = "TestAdmin123!"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the app's StasisDbContext registration (which points at the
            // "DefaultConnection" from appsettings.json, i.e. the real "stasis" database) with
            // one pointed explicitly at the test database connection string above.
            services.RemoveAll<DbContextOptions<StasisDbContext>>();
            services.AddDbContext<StasisDbContext>(options =>
                options.UseNpgsql(ConnectionString).ConfigureWarnings(IgnorePendingModelChangesWarning));
        });

        base.ConfigureWebHost(builder);
    }

    /// <summary>
    /// Builds a <see cref="StasisDbContext"/> directly from <see cref="ConnectionString"/>,
    /// bypassing the factory's DI container. This is deliberate: touching
    /// <see cref="WebApplicationFactory{TEntryPoint}.Services"/> (e.g. via
    /// <c>Services.CreateScope()</c>) lazily builds and starts the test host on first access,
    /// which runs Program.cs's startup seed block. If <see cref="ResetDatabaseAsync"/> went
    /// through <c>Services</c>, calling it before the host has started would itself trigger
    /// startup seeding and then immediately truncate the rows it just created — the opposite of
    /// what a "reset before the test" call is supposed to do. Using a standalone context keeps
    /// migrating/resetting independent of host lifecycle.
    /// </summary>
    private static StasisDbContext CreateStandaloneContext()
    {
        var options = new DbContextOptionsBuilder<StasisDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(IgnorePendingModelChangesWarning)
            .Options;
        return new StasisDbContext(options);
    }

    /// <summary>
    /// Suppresses EF Core's <see cref="RelationalEventId.PendingModelChangesWarning"/>, which
    /// otherwise throws by default when <c>Database.Migrate()</c> runs against a real Npgsql
    /// connection. Confirmed as a false positive for this codebase: both
    /// <c>dotnet ef migrations has-pending-model-changes</c> and <c>dotnet ef migrations add</c>
    /// (which produced an empty Up/Down) agree there is no actual drift between the current
    /// model and the latest migration snapshot. The warning still fires at runtime — this is a
    /// known EF Core/Npgsql quirk where the live-connection code path computes the model
    /// fingerprint slightly differently than the design-time tooling (e.g. Postgres
    /// version-dependent conventions detected only once actually connected). Safe to ignore
    /// here since the design-time tooling is the source of truth for real schema drift.
    ///
    /// HOW THIS WAS VERIFIED (2026-10-21, on this exact codebase state): ran
    /// <c>dotnet ef migrations add DiagnosePendingChanges --project STASIS.csproj</c>
    /// against the real schema/model as they existed at the time, and the generated
    /// migration's <c>Up</c>/<c>Down</c> methods were both completely empty — i.e. the
    /// design-time tooling itself found zero difference between the compiled model and the
    /// latest applied migration snapshot. That throwaway migration was then removed via
    /// <c>dotnet ef migrations remove --force</c>; it was never applied to any database.
    /// <c>dotnet ef migrations has-pending-model-changes</c> independently agreed (reported
    /// no pending changes).
    ///
    /// THIS SUPPRESSION IS BLANKET AND PERMANENT, NOT TIED TO A SPECIFIC MIGRATION — it
    /// will keep silencing this warning for every future model/migration change too, not
    /// just the state verified above. That is a real risk: CLAUDE.md itself flags
    /// "schema and models may drift" as a known gap in this repo (EF Core migrations are not
    /// yet the adopted source of truth; the raw SQL bootstrap script is). If you add or
    /// change a model/migration later and this warning would otherwise have fired, this
    /// suppression means you will NOT be told. Before trusting <c>Database.Migrate()</c>
    /// against this test fixture after such a change, re-run the same empty-diff check:
    /// <c>dotnet ef migrations add &lt;ThrowawayName&gt; --project STASIS.csproj</c> and
    /// confirm the generated <c>Up</c>/<c>Down</c> are empty (then
    /// <c>dotnet ef migrations remove --force</c> to discard it), or run
    /// <c>dotnet ef migrations has-pending-model-changes --project STASIS.csproj</c>. If
    /// either reports real drift, do NOT just re-suppress — fix the drift (add the missing
    /// migration) before relying on this fixture again.
    /// </summary>
    private static void IgnorePendingModelChangesWarning(WarningsConfigurationBuilder warnings) =>
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning);

    /// <summary>
    /// Applies all pending EF Core migrations to the test database. Idempotent — safe to call
    /// more than once, including concurrently across test classes, since <c>Migrate()</c> is a
    /// no-op once the schema is current. Does not touch <see cref="WebApplicationFactory{TEntryPoint}.Services"/>
    /// — see <see cref="CreateStandaloneContext"/> for why.
    /// </summary>
    public async Task MigrateDatabaseAsync()
    {
        using var context = CreateStandaloneContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Resets the test database to a clean, empty-but-migrated state. Ensures migrations have
    /// been applied, then truncates every table in the schema (Identity tables and application
    /// tables alike) using a dynamic <c>TRUNCATE ... CASCADE</c> so it never needs updating as
    /// new tables/migrations are added by later tasks. The EF Core migrations history table is
    /// explicitly excluded so migration state itself isn't wiped.
    ///
    /// Call this from each test (e.g. at the top of its constructor, or from
    /// <c>IAsyncLifetime.InitializeAsync</c>) to avoid cross-test pollution. Deliberately does
    /// not touch <see cref="WebApplicationFactory{TEntryPoint}.Services"/> — see
    /// <see cref="CreateStandaloneContext"/> for why that matters for tests (like
    /// <c>AdminSeedTests</c>) that need to reset the database *before* the test host's own
    /// startup seeding runs.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var context = CreateStandaloneContext();

        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlRawAsync(@"
            DO $$
            DECLARE
                table_list text;
            BEGIN
                SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                INTO table_list
                FROM pg_tables
                WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';

                IF table_list IS NOT NULL THEN
                    EXECUTE format('TRUNCATE TABLE %s RESTART IDENTITY CASCADE', table_list);
                END IF;
            END $$;");
    }
}

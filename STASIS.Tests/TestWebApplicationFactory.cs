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
/// - The first time the underlying test host is created (first <see cref="WebApplicationFactory{TEntryPoint}.Services"/>
///   access, e.g. via <c>CreateClient()</c> or <c>Services.CreateScope()</c>), this factory applies
///   all pending EF Core migrations to the test database via <c>context.Database.Migrate()</c>.
/// - This factory does NOT reset data automatically between tests. xUnit's
///   <c>IClassFixture&lt;TestWebApplicationFactory&gt;</c> creates exactly one instance per test
///   class, shared by every test method in that class. Each test should call
///   <see cref="ResetDatabaseAsync"/> itself (e.g. at the start of its constructor, or from
///   <c>IAsyncLifetime.InitializeAsync</c>) to get a clean, predictable starting point before
///   seeding its own data. This mirrors how later tasks' test suites are expected to use it.
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

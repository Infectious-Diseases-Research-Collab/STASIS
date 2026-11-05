using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using STASIS.Data;
using Xunit;

namespace STASIS.Tests.Migrations;

/// <summary>
/// Verifies the guard added to the <c>20260509184420_BarcodeStudyCompositeKey</c> EF Core
/// migration: before coercing <c>tbl_Specimens."StudyID"</c> to <c>NOT NULL</c> (with a
/// <c>defaultValue</c> of 0) and adding its FK to <c>tbl_Studies</c>, the migration must raise a
/// clear, explicit error if any pre-existing row has <c>StudyID IS NULL</c> -- rather than
/// silently coercing such rows to 0 and then failing the FK-add step with a confusing generic
/// FK-violation error (there being no Study row with id 0).
///
/// This exercises the migration's guard SQL directly against a real Postgres database (required
/// -- the EF Core InMemory provider used by other service-level tests does not support
/// <c>ExecuteSqlRawAsync</c>), rather than invoking <c>Migration.Up</c> through a migrator. The
/// SQL below is copied verbatim from that migration's <c>Up</c> method; if that SQL ever changes,
/// this copy must be updated to match.
///
/// Requires STASIS_TEST_CONNECTION; see <see cref="TestWebApplicationFactory"/>.
/// </summary>
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
            // StudyID is listed explicitly as NULL: the migration's own AlterColumn left a
            // persisted column default of 0 on "StudyID", so merely omitting the column here
            // (as the brief's literal INSERT did) would insert 0 and trip the StudyID FK
            // constraint before the guard SQL ever runs, rather than producing the NULL row
            // this test needs to exercise the guard against.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"tbl_Specimens\" (\"BarcodeID\", \"StudyID\", \"Status\") VALUES ('GUARD-TEST', NULL, 'In-Stock');");

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

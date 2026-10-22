using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using Xunit;

namespace STASIS.Tests.Migrations;

/// <summary>
/// Verifies the data-backfill logic shipped in the
/// <c>20261022063159_BackfillBoxStudyId</c> EF Core migration: it sets
/// <c>tbl_Boxes."StudyID"</c> from the single distinct study of each box's existing specimens,
/// for boxes where exactly one distinct StudyID exists among their specimens, and deliberately
/// leaves <c>StudyID</c> null for boxes whose specimens span more than one distinct study (a
/// real data-integrity problem needing manual cleanup, not something to guess at) or that have
/// zero specimens (genuinely unclaimed).
///
/// This exercises the migration's raw SQL directly against a real Postgres database (required --
/// the EF Core InMemory provider used by other service-level tests does not support
/// <c>ExecuteSqlRawAsync</c>), rather than invoking <c>Migration.Up</c> through a migrator. The
/// SQL below is copied verbatim from that migration's <c>Up</c> method; if that SQL ever changes,
/// this copy must be updated to match. Running it here a second time (migrations already ran
/// once, against an empty database, as part of <see cref="TestWebApplicationFactory.ResetDatabaseAsync"/>)
/// is safe and representative: the statement's own <c>b."StudyID" IS NULL</c> guard makes it
/// idempotent, so re-running it against freshly-seeded "legacy" data exercises exactly the same
/// logic the migration would have applied had that data existed before the migration ran.
///
/// Requires STASIS_TEST_CONNECTION; see <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class BackfillBoxStudyIdMigrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public BackfillBoxStudyIdMigrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Copied verbatim from Migrations/20261022063159_BackfillBoxStudyId.cs's Up() method.
    private const string BackfillSql = @"
        UPDATE ""tbl_Boxes"" b SET ""StudyID"" = s.sid
        FROM (SELECT ""BoxID"", MIN(""StudyID"") AS sid, COUNT(DISTINCT ""StudyID"") AS c
              FROM ""tbl_Specimens"" WHERE ""BoxID"" IS NOT NULL GROUP BY ""BoxID"") s
        WHERE b.""BoxID"" = s.""BoxID"" AND s.c = 1 AND b.""StudyID"" IS NULL;";

    private static async Task<Study> AddStudyAsync(StasisDbContext context, string code)
    {
        var study = new Study { StudyCode = code };
        context.Studies.Add(study);
        await context.SaveChangesAsync();
        return study;
    }

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label)
    {
        // StudyID left null -- simulating a pre-existing box from before the column existed.
        var box = new Box { BoxLabel = label, BoxType = "81-slot", BoxCategory = "Standard" };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }

    private static async Task AddSpecimenAsync(StasisDbContext context, string barcode, int studyId, int boxId)
    {
        context.Specimens.Add(new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            BoxID = boxId,
            Status = "In-Stock"
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Backfill_BoxWhoseSpecimensShareOneStudy_SetsStudyIdToThatStudy()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var studyA = await AddStudyAsync(context, "BF-SINGLE");
        var box = await AddBoxAsync(context, "BF-BOX-SINGLE");
        await AddSpecimenAsync(context, "BF-S1", studyA.StudyID, box.BoxID);
        await AddSpecimenAsync(context, "BF-S2", studyA.StudyID, box.BoxID);

        await context.Database.ExecuteSqlRawAsync(BackfillSql);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(studyA.StudyID, reloaded.StudyID);
    }

    [Fact]
    public async Task Backfill_BoxWhoseSpecimensSpanTwoStudies_LeavesStudyIdNull()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var studyA = await AddStudyAsync(context, "BF-MIXED-A");
        var studyB = await AddStudyAsync(context, "BF-MIXED-B");
        var box = await AddBoxAsync(context, "BF-BOX-MIXED");
        await AddSpecimenAsync(context, "BF-S3", studyA.StudyID, box.BoxID);
        await AddSpecimenAsync(context, "BF-S4", studyB.StudyID, box.BoxID);

        await context.Database.ExecuteSqlRawAsync(BackfillSql);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.StudyID);
    }

    [Fact]
    public async Task Backfill_BoxWithZeroSpecimens_LeavesStudyIdNull()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var box = await AddBoxAsync(context, "BF-BOX-EMPTY");

        await context.Database.ExecuteSqlRawAsync(BackfillSql);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.StudyID);
    }

    [Fact]
    public async Task Backfill_BoxAlreadyHavingStudyId_IsNotOverwritten()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var studyA = await AddStudyAsync(context, "BF-ALREADY-A");
        var studyB = await AddStudyAsync(context, "BF-ALREADY-B");

        var box = new Box { BoxLabel = "BF-BOX-ALREADY", BoxType = "81-slot", BoxCategory = "Standard", StudyID = studyA.StudyID };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();

        // Specimen belongs to a different study than the box already claims -- the guard
        // (b."StudyID" IS NULL) must leave the box's pre-existing StudyID untouched rather than
        // overwriting it based on specimen contents.
        await AddSpecimenAsync(context, "BF-S5", studyB.StudyID, box.BoxID);

        await context.Database.ExecuteSqlRawAsync(BackfillSql);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(studyA.StudyID, reloaded.StudyID);
    }
}

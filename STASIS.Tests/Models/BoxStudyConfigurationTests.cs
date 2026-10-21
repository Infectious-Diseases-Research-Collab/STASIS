using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Tests.TestHelpers;
using Xunit;

namespace STASIS.Tests.Models;

public class BoxStudyConfigurationTests
{
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

    [Fact]
    public async Task Box_StudyID_CanBeNull()
    {
        var context = TestDbContextFactory.Create();

        var box = new Box { BoxLabel = "B2", BoxType = "81-slot", StudyID = null };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();

        var reloaded = await context.Boxes.FirstAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.StudyID);
    }
}

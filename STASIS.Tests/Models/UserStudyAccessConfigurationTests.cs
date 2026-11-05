using System.Linq;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Tests.TestHelpers;
using Xunit;

namespace STASIS.Tests.Models;

public class UserStudyAccessConfigurationTests
{
    // NOTE: The brief for this test specified asserting a DbUpdateException from
    // a duplicate (AspNetUserId, StudyID) SaveChangesAsync call, on the premise
    // that "The EF Core InMemory provider does enforce unique indexes." That
    // premise does not hold for Microsoft.EntityFrameworkCore.InMemory 10.0.1:
    // empirically, HasIndex(...).IsUnique() is NOT enforced at SaveChanges time
    // by the InMemory provider (only primary/alternate keys are enforced by the
    // change tracker, and that throws InvalidOperationException at Add() time,
    // not DbUpdateException at SaveChanges time). See task-1-report.md for the
    // repro. This test instead asserts the unique index is actually present in
    // the EF model metadata, which is what the InMemory provider can reliably
    // verify; real duplicate-rejection behavior against Postgres is covered by
    // the Task 15 integration test, as the brief also anticipated for the
    // Role check constraint.
    [Fact]
    public void UserStudyAccess_HasUniqueIndexOnAspNetUserIdAndStudyID()
    {
        var context = TestDbContextFactory.Create();
        var entityType = context.Model.FindEntityType(typeof(UserStudyAccess))!;

        var index = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(UserStudyAccess.AspNetUserId), nameof(UserStudyAccess.StudyID) }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void UserStudyAccess_HasNonUniqueIndexOnStudyID()
    {
        var context = TestDbContextFactory.Create();
        var entityType = context.Model.FindEntityType(typeof(UserStudyAccess))!;

        var index = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(UserStudyAccess.StudyID) }));

        Assert.NotNull(index);
        Assert.False(index!.IsUnique);
    }

    [Fact]
    public async Task UserStudyAccess_CanBeInsertedAndRetrieved()
    {
        var context = TestDbContextFactory.Create();
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = "u1",
            StudyID = 1,
            Role = "Viewer",
            GrantedByUserId = "admin",
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var saved = await context.UserStudyAccesses.SingleAsync();
        Assert.Equal("u1", saved.AspNetUserId);
        Assert.Equal(1, saved.StudyID);
        Assert.Equal("Viewer", saved.Role);
        Assert.Equal("admin", saved.GrantedByUserId);
    }
}

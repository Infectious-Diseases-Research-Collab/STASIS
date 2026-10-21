using Microsoft.EntityFrameworkCore;
using STASIS.Data;

namespace STASIS.Tests.TestHelpers;

/// <summary>
/// Builds an in-memory <see cref="StasisDbContext"/> for unit tests.
/// Each call gets its own isolated database instance.
/// </summary>
public static class TestDbContextFactory
{
    public static StasisDbContext Create()
    {
        var options = new DbContextOptionsBuilder<StasisDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StasisDbContext(options);
    }
}

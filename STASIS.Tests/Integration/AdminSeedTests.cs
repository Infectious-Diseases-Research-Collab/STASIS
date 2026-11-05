using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies Program.cs's startup seed block only creates the "Admin" Identity role — the
/// global "Write"/"Read" roles that used to be seeded unconditionally are no longer created.
/// Requires STASIS_TEST_CONNECTION to be set; see <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class AdminSeedTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public AdminSeedTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Reset BEFORE touching _factory.Services anywhere else in the test, so the test host's
        // own startup seed block (triggered by the first Services access below) runs against a
        // known-clean, freshly migrated database.
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Startup_SeedsOnlyAdminRole_NotWriteOrRead()
    {
        // First access of Services lazily builds and starts the test host, which runs
        // Program.cs's admin/role seed block (AdminSeedPassword is configured by the factory).
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.False(await roleManager.RoleExistsAsync("Write"));
        Assert.False(await roleManager.RoleExistsAsync("Read"));
    }
}

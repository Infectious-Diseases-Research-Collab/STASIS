using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;

namespace STASIS.Tests.TestHelpers;

/// <summary>
/// Builds a real <see cref="UserManager{TUser}"/> / <see cref="RoleManager{TRole}"/> pair
/// backed by a given in-memory <see cref="StasisDbContext"/>, so tests exercise actual
/// ASP.NET Core Identity behavior (e.g. <c>IsInRoleAsync</c>) rather than a hand-rolled stand-in.
/// </summary>
public static class IdentityTestFactory
{
    public static (UserManager<IdentityUser> UserManager, RoleManager<IdentityRole> RoleManager) Create(StasisDbContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<StasisDbContext>();

        var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        return (userManager, roleManager);
    }
}

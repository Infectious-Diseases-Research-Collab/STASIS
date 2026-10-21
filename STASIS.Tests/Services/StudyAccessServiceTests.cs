using Microsoft.AspNetCore.Identity;
using STASIS.Models;
using STASIS.Services;
using STASIS.Tests.TestHelpers;
using Xunit;

namespace STASIS.Tests.Services;

public class StudyAccessServiceTests
{
    [Fact]
    public async Task SystemAdmin_IsVisibleEverywhere_WithoutAssignment()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, roleManager) = IdentityTestFactory.Create(context);

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var admin = new IdentityUser { UserName = "admin@test.com", Email = "admin@test.com" };
        await userManager.CreateAsync(admin);
        await userManager.AddToRoleAsync(admin, "Admin");

        const int studyId = 42; // no UserStudyAccess row exists for this study/user

        var service = new StudyAccessService(context, userManager);

        Assert.True(await service.IsSystemAdminAsync(admin.Id));
        Assert.True(await service.CanViewAsync(admin.Id, studyId));
        Assert.True(await service.CanEditAsync(admin.Id, studyId));
        Assert.True(await service.CanManageStudyAsync(admin.Id, studyId));
    }

    [Fact]
    public async Task Viewer_CanView_ButNotEdit()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, _) = IdentityTestFactory.Create(context);

        var viewer = new IdentityUser { UserName = "viewer@test.com", Email = "viewer@test.com" };
        await userManager.CreateAsync(viewer);

        const int studyId = 1;
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = viewer.Id,
            StudyID = studyId,
            Role = "Viewer",
            GrantedByUserId = "admin",
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new StudyAccessService(context, userManager);

        Assert.Equal(StudyRole.Viewer, await service.GetRoleAsync(viewer.Id, studyId));
        Assert.True(await service.CanViewAsync(viewer.Id, studyId));
        Assert.False(await service.CanEditAsync(viewer.Id, studyId));
        Assert.False(await service.CanManageStudyAsync(viewer.Id, studyId));
    }

    [Fact]
    public async Task Editor_CanViewAndEdit()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, _) = IdentityTestFactory.Create(context);

        var editor = new IdentityUser { UserName = "editor@test.com", Email = "editor@test.com" };
        await userManager.CreateAsync(editor);

        const int studyId = 1;
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = editor.Id,
            StudyID = studyId,
            Role = "Editor",
            GrantedByUserId = "admin",
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new StudyAccessService(context, userManager);

        Assert.Equal(StudyRole.Editor, await service.GetRoleAsync(editor.Id, studyId));
        Assert.True(await service.CanViewAsync(editor.Id, studyId));
        Assert.True(await service.CanEditAsync(editor.Id, studyId));
        Assert.False(await service.CanManageStudyAsync(editor.Id, studyId));
    }

    [Fact]
    public async Task StudyAdmin_CanEditAndManage()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, _) = IdentityTestFactory.Create(context);

        var studyAdmin = new IdentityUser { UserName = "studyadmin@test.com", Email = "studyadmin@test.com" };
        await userManager.CreateAsync(studyAdmin);

        const int studyId = 1;
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = studyAdmin.Id,
            StudyID = studyId,
            Role = "StudyAdmin",
            GrantedByUserId = "admin",
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new StudyAccessService(context, userManager);

        Assert.Equal(StudyRole.StudyAdmin, await service.GetRoleAsync(studyAdmin.Id, studyId));
        Assert.True(await service.CanViewAsync(studyAdmin.Id, studyId));
        Assert.True(await service.CanEditAsync(studyAdmin.Id, studyId));
        Assert.True(await service.CanManageStudyAsync(studyAdmin.Id, studyId));
    }

    [Fact]
    public async Task NoAssignment_CannotViewOrEdit_AndIsNotSystemAdmin()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, _) = IdentityTestFactory.Create(context);

        var staff = new IdentityUser { UserName = "staff@test.com", Email = "staff@test.com" };
        await userManager.CreateAsync(staff);

        const int studyId = 1;

        var service = new StudyAccessService(context, userManager);

        Assert.False(await service.IsSystemAdminAsync(staff.Id));
        Assert.Equal(StudyRole.None, await service.GetRoleAsync(staff.Id, studyId));
        Assert.False(await service.CanViewAsync(staff.Id, studyId));
        Assert.False(await service.CanEditAsync(staff.Id, studyId));
        Assert.False(await service.CanManageStudyAsync(staff.Id, studyId));
    }

    [Fact]
    public async Task GetVisibleStudyIdsAsync_ReturnsNull_ForSystemAdmin()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, roleManager) = IdentityTestFactory.Create(context);

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var admin = new IdentityUser { UserName = "admin2@test.com", Email = "admin2@test.com" };
        await userManager.CreateAsync(admin);
        await userManager.AddToRoleAsync(admin, "Admin");

        var service = new StudyAccessService(context, userManager);

        Assert.Null(await service.GetVisibleStudyIdsAsync(admin.Id));
    }

    [Fact]
    public async Task GetVisibleStudyIdsAsync_ReturnsExactAssignedSet_ForStaff()
    {
        var context = TestDbContextFactory.Create();
        var (userManager, _) = IdentityTestFactory.Create(context);

        var staff = new IdentityUser { UserName = "staff2@test.com", Email = "staff2@test.com" };
        await userManager.CreateAsync(staff);

        var otherStaff = new IdentityUser { UserName = "other@test.com", Email = "other@test.com" };
        await userManager.CreateAsync(otherStaff);

        context.UserStudyAccesses.AddRange(
            new UserStudyAccess { AspNetUserId = staff.Id, StudyID = 1, Role = "Viewer", GrantedByUserId = "admin", GrantedDate = DateTime.UtcNow },
            new UserStudyAccess { AspNetUserId = staff.Id, StudyID = 2, Role = "Editor", GrantedByUserId = "admin", GrantedDate = DateTime.UtcNow },
            new UserStudyAccess { AspNetUserId = staff.Id, StudyID = 3, Role = "StudyAdmin", GrantedByUserId = "admin", GrantedDate = DateTime.UtcNow },
            // Belongs to a different user — must not leak into staff's visible set.
            new UserStudyAccess { AspNetUserId = otherStaff.Id, StudyID = 4, Role = "Viewer", GrantedByUserId = "admin", GrantedDate = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new StudyAccessService(context, userManager);

        var visible = await service.GetVisibleStudyIdsAsync(staff.Id);

        Assert.NotNull(visible);
        Assert.Equal(new[] { 1, 2, 3 }, visible!.OrderBy(id => id));
    }
}

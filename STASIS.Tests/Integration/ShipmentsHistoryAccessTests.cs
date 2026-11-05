using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages.Shipments;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies <see cref="HistoryModel"/> (<c>Pages/Shipments/History.cshtml.cs</c>) enforces study
/// access server-side. A batch's studies are the studies of its matched specimens.
/// - Viewing a batch or its manifest needs view access to every study in it. A batch with no
///   matched specimens is visible only to whoever imported it (and System Admins).
/// - Recording an approval also needs the "Can Approve Shipments" flag (or System Admin).
/// - Shipping needs Editor on every study in the batch, and the batch must be Approved.
/// Before this, any logged-in user could see, approve and ship every batch.
/// Requires STASIS_TEST_CONNECTION; see <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class ShipmentsHistoryAccessTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public ShipmentsHistoryAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static HistoryModel BuildModel(IServiceProvider services, string callerUserId)
    {
        return new HistoryModel(
            services.GetRequiredService<IShipmentService>(),
            services.GetRequiredService<IStudyAccessService>())
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, callerUserId) },
                        "TestAuth"))
                }
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<ITempDataProvider>())
        };
    }

    private static async Task<IdentityUser> CreateUserAsync(IServiceProvider services, string email, bool systemAdmin = false)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email };
        await userManager.CreateAsync(user);

        if (systemAdmin)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            await userManager.AddToRoleAsync(user, "Admin");
        }

        return user;
    }

    private static async Task GrantAsync(StasisDbContext context, string userId, int studyId, string role, string grantedBy)
    {
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = userId,
            StudyID = studyId,
            Role = role,
            GrantedByUserId = grantedBy,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task SetCanApproveShipmentsAsync(StasisDbContext context, string userId)
    {
        context.UserProfiles.Add(new UserProfile { AspNetUserId = userId, CanApproveShipments = true });
        await context.SaveChangesAsync();
    }

    private sealed record Fixture(
        IdentityUser SysAdmin, Study StudyA, Study StudyB,
        ShipmentBatch BatchA, ShipmentBatch BatchAB, ShipmentBatch BatchUnmatched,
        Specimen SpecimenA);

    // BatchA holds a study-A specimen. BatchAB holds one specimen from each study. BatchUnmatched
    // has only an unknown barcode and was imported by the System Admin.
    private static async Task<Fixture> SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<StasisDbContext>();
        var shipmentService = services.GetRequiredService<IShipmentService>();

        var sysAdmin = await CreateUserAsync(services, "sysadmin-hist@test.com", systemAdmin: true);

        var studyA = new Study { StudyCode = "HIST-A" };
        var studyB = new Study { StudyCode = "HIST-B" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var specimenA = new Specimen { BarcodeID = "HIST-SA", StudyID = studyA.StudyID, Status = "In-Stock" };
        context.Specimens.AddRange(
            specimenA,
            new Specimen { BarcodeID = "HIST-SA2", StudyID = studyA.StudyID, Status = "In-Stock" },
            new Specimen { BarcodeID = "HIST-SB", StudyID = studyB.StudyID, Status = "In-Stock" });
        await context.SaveChangesAsync();

        async Task<ShipmentBatch> ImportAsync(string csv)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
            return await shipmentService.ImportBatchFromCsvAsync(stream, sysAdmin.Id, null, null, null);
        }

        var batchA = await ImportAsync("Barcode\nHIST-SA");
        var batchAB = await ImportAsync("Barcode\nHIST-SA2\nHIST-SB");
        var batchUnmatched = await ImportAsync("Barcode\nHIST-NOPE");

        return new Fixture(sysAdmin, studyA, studyB, batchA, batchAB, batchUnmatched, specimenA);
    }

    private static async Task ApproveAllLevelsAsync(StasisDbContext context, int batchId)
    {
        var batch = await context.ShipmentBatches.Include(b => b.Approval).SingleAsync(b => b.BatchID == batchId);
        batch.Status = "Approved";
        batch.Approval!.EDApprovalStatus = "Approved";
        batch.Approval.RegulatoryApprovalStatus = "Approved";
        batch.Approval.PIApprovalStatus = "Approved";
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task OnGet_ViewerOnStudyA_SeesOnlyBatchesWhollyInStudyA()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var viewer = await CreateUserAsync(services, "viewer-hist1@test.com");
        await GrantAsync(context, viewer.Id, f.StudyA.StudyID, "Viewer", f.SysAdmin.Id);

        var model = BuildModel(services, viewer.Id);
        await model.OnGetAsync();

        Assert.Equal(new[] { f.BatchA.BatchID }, model.Batches.Select(b => b.BatchID).ToArray());
    }

    [Fact]
    public async Task OnGet_SystemAdmin_SeesAllBatches()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var f = await SeedAsync(services);

        var model = BuildModel(services, f.SysAdmin.Id);
        await model.OnGetAsync();

        Assert.Equal(3, model.Batches.Count);
    }

    [Fact]
    public async Task OnGet_UserWithNoAccess_SeesNoBatches_AndCannotOpenOne()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var f = await SeedAsync(services);

        var outsider = await CreateUserAsync(services, "outsider-hist@test.com");

        var model = BuildModel(services, outsider.Id);
        await model.OnGetAsync();
        Assert.Empty(model.Batches);

        var detailModel = BuildModel(services, outsider.Id);
        detailModel.BatchId = f.BatchA.BatchID;
        Assert.IsType<NotFoundResult>(await detailModel.OnGetAsync());
        Assert.Null(detailModel.SelectedBatch);
    }

    [Fact]
    public async Task OnGet_BatchWithAStudyTheCallerCannotView_ReturnsNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var viewer = await CreateUserAsync(services, "viewer-hist2@test.com");
        await GrantAsync(context, viewer.Id, f.StudyA.StudyID, "Viewer", f.SysAdmin.Id);

        var model = BuildModel(services, viewer.Id);
        model.BatchId = f.BatchAB.BatchID;

        Assert.IsType<NotFoundResult>(await model.OnGetAsync());
        Assert.Null(model.SelectedBatch);
    }

    [Fact]
    public async Task OnGet_BatchWithNoMatchedSpecimens_VisibleToImporterOnly()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var editor = await CreateUserAsync(services, "editor-hist-unmatched@test.com");
        await GrantAsync(context, editor.Id, f.StudyA.StudyID, "Editor", f.SysAdmin.Id);

        var model = BuildModel(services, editor.Id);
        model.BatchId = f.BatchUnmatched.BatchID;
        Assert.IsType<NotFoundResult>(await model.OnGetAsync());

        var importerModel = BuildModel(services, f.SysAdmin.Id);
        importerModel.BatchId = f.BatchUnmatched.BatchID;
        Assert.IsType<PageResult>(await importerModel.OnGetAsync());
        Assert.NotNull(importerModel.SelectedBatch);
    }

    [Fact]
    public async Task OnPostApprove_ViewerWithoutApproveFlag_IsRejected_ApprovalUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var editor = await CreateUserAsync(services, "editor-hist-noflag@test.com");
        await GrantAsync(context, editor.Id, f.StudyA.StudyID, "Editor", f.SysAdmin.Id);

        var model = BuildModel(services, editor.Id);
        model.BatchId = f.BatchA.BatchID;
        model.ApprovalLevel = "ED";
        model.ApprovalStatus = "Approved";

        var result = await model.OnPostApproveAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(model.TempData["Error"]);
        var approval = await context.ShipmentBatches.AsNoTracking().Where(b => b.BatchID == f.BatchA.BatchID)
            .Select(b => b.Approval!).SingleAsync();
        Assert.Null(approval.EDApprovalStatus);
    }

    [Fact]
    public async Task OnPostApprove_ApproveFlagButCannotViewBatch_ReturnsNotFound_ApprovalUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var approver = await CreateUserAsync(services, "approver-hist-a@test.com");
        await GrantAsync(context, approver.Id, f.StudyA.StudyID, "Viewer", f.SysAdmin.Id);
        await SetCanApproveShipmentsAsync(context, approver.Id);

        var model = BuildModel(services, approver.Id);
        model.BatchId = f.BatchAB.BatchID;
        model.ApprovalLevel = "ED";
        model.ApprovalStatus = "Approved";

        Assert.IsType<NotFoundResult>(await model.OnPostApproveAsync());

        var approval = await context.ShipmentBatches.AsNoTracking().Where(b => b.BatchID == f.BatchAB.BatchID)
            .Select(b => b.Approval!).SingleAsync();
        Assert.Null(approval.EDApprovalStatus);
    }

    [Fact]
    public async Task OnPostApprove_ApproveFlagAndCanViewBatch_RecordsApproval()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var approver = await CreateUserAsync(services, "approver-hist-ok@test.com");
        await GrantAsync(context, approver.Id, f.StudyA.StudyID, "Viewer", f.SysAdmin.Id);
        await SetCanApproveShipmentsAsync(context, approver.Id);

        var model = BuildModel(services, approver.Id);
        model.BatchId = f.BatchA.BatchID;
        model.ApprovalLevel = "ED";
        model.ApprovalStatus = "Approved";

        Assert.IsType<RedirectToPageResult>(await model.OnPostApproveAsync());

        var approval = await context.ShipmentBatches.AsNoTracking().Where(b => b.BatchID == f.BatchA.BatchID)
            .Select(b => b.Approval!).SingleAsync();
        Assert.Equal("Approved", approval.EDApprovalStatus);
        Assert.Equal(approver.Id, approval.EDApproverUserId);
    }

    [Fact]
    public async Task OnPostShip_ViewerOnly_IsRejected_SpecimenNotShipped()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);
        await ApproveAllLevelsAsync(context, f.BatchA.BatchID);

        var viewer = await CreateUserAsync(services, "viewer-hist-ship@test.com");
        await GrantAsync(context, viewer.Id, f.StudyA.StudyID, "Viewer", f.SysAdmin.Id);

        var model = BuildModel(services, viewer.Id);
        model.BatchId = f.BatchA.BatchID;
        model.Courier = "DHL";

        var result = await model.OnPostShipAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(model.TempData["Error"]);
        Assert.Equal("In-Stock", (await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == f.SpecimenA.SpecimenID)).Status);
        Assert.False(await context.Shipments.AnyAsync(s => s.BatchID == f.BatchA.BatchID));
    }

    [Fact]
    public async Task OnPostShip_EditorOnOnlyOneOfTheBatchStudies_ReturnsNotFound_NothingShipped()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);
        await ApproveAllLevelsAsync(context, f.BatchAB.BatchID);

        var editor = await CreateUserAsync(services, "editor-hist-ab@test.com");
        await GrantAsync(context, editor.Id, f.StudyA.StudyID, "Editor", f.SysAdmin.Id);

        var model = BuildModel(services, editor.Id);
        model.BatchId = f.BatchAB.BatchID;
        model.Courier = "DHL";

        Assert.IsType<NotFoundResult>(await model.OnPostShipAsync());
        Assert.False(await context.Shipments.AnyAsync(s => s.BatchID == f.BatchAB.BatchID));
    }

    [Fact]
    public async Task OnPostShip_UnapprovedBatch_IsRejected_EvenForSystemAdmin()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var model = BuildModel(services, f.SysAdmin.Id);
        model.BatchId = f.BatchA.BatchID;
        model.Courier = "DHL";

        await model.OnPostShipAsync();

        Assert.Contains(model.ShipmentErrors, e => e.Contains("approved", StringComparison.OrdinalIgnoreCase));
        Assert.False(await context.Shipments.AnyAsync(s => s.BatchID == f.BatchA.BatchID));
    }

    [Fact]
    public async Task OnPostShip_EditorOnEveryStudyInApprovedBatch_Ships()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);
        await ApproveAllLevelsAsync(context, f.BatchA.BatchID);

        var editor = await CreateUserAsync(services, "editor-hist-ok@test.com");
        await GrantAsync(context, editor.Id, f.StudyA.StudyID, "Editor", f.SysAdmin.Id);

        var model = BuildModel(services, editor.Id);
        model.BatchId = f.BatchA.BatchID;
        model.Courier = "DHL";

        Assert.IsType<RedirectToPageResult>(await model.OnPostShipAsync());
        Assert.Equal("Shipped", (await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == f.SpecimenA.SpecimenID)).Status);
    }

    [Fact]
    public async Task OnGetManifest_ForBatchTheCallerCannotView_ReturnsNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();
        var f = await SeedAsync(services);

        var shipment = new Shipment { BatchID = f.BatchAB.BatchID, ShipmentDate = DateTime.UtcNow, Courier = "DHL", ShippedByUserId = f.SysAdmin.Id };
        context.Shipments.Add(shipment);
        await context.SaveChangesAsync();

        var viewer = await CreateUserAsync(services, "viewer-hist-manifest@test.com");
        await GrantAsync(context, viewer.Id, f.StudyA.StudyID, "Viewer", f.SysAdmin.Id);

        Assert.IsType<NotFoundResult>(await BuildModel(services, viewer.Id).OnGetManifestAsync(shipment.ShipmentID));
        Assert.IsType<FileContentResult>(await BuildModel(services, f.SysAdmin.Id).OnGetManifestAsync(shipment.ShipmentID));
    }
}

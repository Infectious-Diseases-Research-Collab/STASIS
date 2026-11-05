using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<StasisDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<StasisDbContext>();

// ASP.NET Core Identity's cookie auth bakes the signed-in user's role claims (e.g. "Admin") into
// the encrypted auth cookie at sign-in time. By default, SecurityStampValidator only re-checks
// that cookie against the database (and refreshes the principal's claims if anything changed)
// every 30 minutes -- IdentityOptions in this ASP.NET Core version has no
// SecurityStampValidationInterval property of its own, so this is configured via
// SecurityStampValidatorOptions directly. Without this, revoking a user's System Admin role (or
// any other role-claim-based access) does not actually take effect until that interval elapses
// or the user logs in again -- confirmed empirically against this exact app (a System Admin
// stripped of the "Admin" role mid-session kept getting 200 OK from an
// [Authorize(Roles = "Admin")]-gated page on the very next request, using the same cookie, with
// the default interval). Setting this to zero forces re-validation (and a fresh principal/claims
// pull from the database) on every single request.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Email Configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailSender>();

builder.Services.AddScoped<ISampleService, SampleService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ILabSetupService, LabSetupService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddScoped<IStudyAccessService, StudyAccessService>();

builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        options.Filters.Add<STASIS.Services.PasswordChangeFilter>();
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Seed Admin User
// Requires AdminSeedPassword in configuration (user secrets, env var, or appsettings).
// Set via: dotnet user-secrets set "AdminSeedPassword" "YourSecurePassword"
using (var scope = app.Services.CreateScope())
{
    var adminPassword = app.Configuration["AdminSeedPassword"];
    if (string.IsNullOrEmpty(adminPassword))
    {
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        startupLogger.LogWarning(
            "AdminSeedPassword is not configured. The admin account will not be seeded. " +
            "Set it with: dotnet user-secrets set \"AdminSeedPassword\" \"YourSecurePassword\"");
    }
    else
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure all roles exist
        foreach (var role in new[] { "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed admin user
        var adminEmail = "admin@stasis.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            await userManager.CreateAsync(adminUser, adminPassword);
        }

        // Ensure admin user has Admin role
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

app.Run();

// Exposes the implicit Program class generated for this top-level-statements file so that
// WebApplicationFactory<Program> (used by STASIS.Tests' TestWebApplicationFactory) can
// reference it from the test project.
public partial class Program { }

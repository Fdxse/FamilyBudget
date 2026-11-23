using FamilyBudget.Web.Components;
using FamilyBudget.Web.Data;
using FamilyBudget.Web.Security;
using FamilyBudget.Web.Services;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// The app is in Swedish: format dates and numbers the Swedish way everywhere.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("sv-SE");

var builder = WebApplication.CreateBuilder(args);

// --- Database -------------------------------------------------------------
// The connection string comes from configuration. In Docker it is supplied via the
// environment variable ConnectionStrings__FamilyBudget (see docker-compose.yml).
var connectionString = builder.Configuration.GetConnectionString("FamilyBudget")
    ?? throw new InvalidOperationException("Connection string 'FamilyBudget' is missing.");

// We pin the MariaDB version instead of auto-detecting it, so the app can start
// even if the database container is not ready yet (auto-detect needs a live connection).
var mariaDbVersionText = builder.Configuration["Database:MariaDbVersion"] ?? "11.4";
var mariaDbServerVersion = new MariaDbServerVersion(Version.Parse(mariaDbVersionText));

// A DbContext factory is the recommended pattern for Blazor Server, where one
// circuit (browser tab) lives a long time and should not share one DbContext.
builder.Services.AddDbContextFactory<FamilyBudgetDbContext>(options =>
    options.UseMySql(connectionString, mariaDbServerVersion));

// --- Budget services ------------------------------------------------------
builder.Services.AddScoped<BudgetGroupAccess>();
// Shared outcome calculation for year overview, budget and statistics pages.
builder.Services.AddScoped<BudgetOutcomeCalculator>();
// Builds the .xlsx files for "Ladda ner till Excel".
builder.Services.AddScoped<ExcelExporter>();

// --- Login (ASP.NET Core Identity) -----------------------------------------
// Users log in with a user name and password; the session is kept in a cookie.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Family app: long passwords are better than complicated rules.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;

        // Allow Swedish letters in user names.
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzåäöABCDEFGHIJKLMNOPQRSTUVWXYZÅÄÖ0123456789-._@+";

        // Lock an account for 5 minutes after 5 wrong passwords in a row.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<FamilyBudgetDbContext>()
    .AddSignInManager()
    .AddErrorDescriber<SwedishIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

// Used to cache whether a user has two-step verification (see TwoFactorRequirement).
builder.Services.AddMemoryCache();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/konto/logga-in";
    options.AccessDeniedPath = "/konto/ingen-behorighet";
    // Stay logged in for 30 days of inactivity on a device.
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// "Kom ihåg den här enheten" after a two-step login lasts 30 days.
builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
    IdentityConstants.TwoFactorRememberMeScheme, options => options.ExpireTimeSpan = TimeSpan.FromDays(30));

// --- Data protection ------------------------------------------------------
// Keys used for cookies/antiforgery are stored on disk so logins survive container
// restarts. In Docker this folder is mounted from the NAS share.
var dataProtectionKeyFolder = builder.Configuration["DataProtection:KeyFolder"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyFolder))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("FamilyBudget")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyFolder));
}

// --- Health check ---------------------------------------------------------
// /health answers "Healthy" when the app is up and can reach the database.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// --- Blazor ---------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The app usually runs behind Synology's reverse proxy, which terminates HTTPS.
// Trust the X-Forwarded-* headers so the app knows the original scheme and host.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The proxy sits on the private network, so we do not restrict known proxies here.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Apply any pending database migrations at startup (waits for MariaDB to come up).
await DatabaseMigrator.MigrateWithRetryAsync(app.Services, app.Logger);

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// HTTPS is handled by the reverse proxy on the NAS, so redirect only when asked to.
if (app.Configuration.GetValue<bool>("UseHttpsRedirection"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
// Optionally force every user to turn on two-step verification (Security:RequireTwoFactor).
app.UseTwoFactorRequirement();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");

// Logging out is a POST (from the form in the menu) so a link cannot log you out by accident.
app.MapPost("/konto/logga-ut", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/konto/logga-in");
});
// "Ladda ner till Excel": the whole period, or one year with ?ar=2026. Only members of the group get the file.
app.MapGet("/grupp/{groupId:int}/excel", async (int groupId, int? ar, HttpContext httpContext,
    BudgetGroupAccess groupAccess, ExcelExporter excelExporter) =>
{
    var budgetGroup = await groupAccess.GetGroupForMemberAsync(httpContext.User, groupId);
    if (budgetGroup is null)
    {
        return Results.NotFound();
    }
    var onlyYear = ar is >= 1900 and <= 2200 ? ar : null;
    var workbookBytes = await excelExporter.CreateWorkbookAsync(budgetGroup, onlyYear);
    var fileName = $"{budgetGroup.Name} {(onlyYear?.ToString() ?? "alla år")} {DateTime.Today:yyyy-MM-dd}.xlsx";
    return Results.File(workbookBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

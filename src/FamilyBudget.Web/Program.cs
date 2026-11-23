using FamilyBudget.Web.Components;
using FamilyBudget.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

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

app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

using FamilyBudget.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace FamilyBudget.Web.Security;

/// <summary>
/// When Security:RequireTwoFactor is true (REQUIRE_TWO_FACTOR=true in .env), logged-in users who have
/// not turned on two-step verification are sent to the setup page until they do.
/// Meant to be switched on before the app is reachable from the internet.
/// </summary>
public static class TwoFactorRequirement
{
    /// <summary>Page where users turn on two-step verification.</summary>
    public const string SetupPath = "/konto/tvastegsverifiering";

    // How long the "has two-step verification" answer is cached per user, to avoid a database call per request.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public static bool IsRequired(IConfiguration configuration) =>
        configuration.GetValue<bool>("Security:RequireTwoFactor");

    /// <summary>Cache key for one user's two-step status.</summary>
    public static string CacheKey(string userId) => $"two-factor-enabled:{userId}";

    /// <summary>Middleware that redirects users without two-step verification to <see cref="SetupPath"/>.</summary>
    public static IApplicationBuilder UseTwoFactorRequirement(this IApplicationBuilder app)
    {
        return app.Use(async (httpContext, next) =>
        {
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            if (!IsRequired(configuration)
                || httpContext.User.Identity?.IsAuthenticated != true
                || IsAlwaysAllowed(httpContext.Request.Path))
            {
                await next();
                return;
            }

            var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var memoryCache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var userId = userManager.GetUserId(httpContext.User);
            if (userId is null)
            {
                await next();
                return;
            }

            var hasTwoFactor = await memoryCache.GetOrCreateAsync(CacheKey(userId), async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = CacheDuration;
                var user = await userManager.FindByIdAsync(userId);
                return user is not null && await userManager.GetTwoFactorEnabledAsync(user);
            });

            if (hasTwoFactor)
            {
                await next();
                return;
            }
            httpContext.Response.Redirect($"{SetupPath}?krav=1");
        });
    }

    /// <summary>Account pages, Blazor's own files and static files must work before two-step is set up.</summary>
    private static bool IsAlwaysAllowed(PathString requestPath)
    {
        var path = requestPath.Value ?? string.Empty;
        return path.StartsWith("/konto/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_", StringComparison.Ordinal)          // _framework, _blazor, _content
            || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || Path.HasExtension(path);                                   // css, js, images
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FamilyBudget.Web.Data;

/// <summary>
/// Health check that reports Healthy only when the database answers.
/// Used by Docker's HEALTHCHECK and by the update script.
/// </summary>
public class DatabaseHealthCheck(IDbContextFactory<FamilyBudgetDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var databaseContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        var canConnect = await databaseContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("Database reachable.")
            : HealthCheckResult.Unhealthy("Cannot connect to the database.");
    }
}

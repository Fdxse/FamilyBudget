using Microsoft.EntityFrameworkCore;

namespace FamilyBudget.Web.Data;

/// <summary>
/// Applies EF Core migrations at application startup.
/// MariaDB may still be starting when the app container starts, so we retry for a while.
/// </summary>
public static class DatabaseMigrator
{
    private const int MaximumAttempts = 30;
    private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromSeconds(2);

    public static async Task MigrateWithRetryAsync(IServiceProvider services, ILogger logger)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<FamilyBudgetDbContext>>();

        for (var attemptNumber = 1; ; attemptNumber++)
        {
            try
            {
                await using var databaseContext = await contextFactory.CreateDbContextAsync();
                await databaseContext.Database.MigrateAsync();
                logger.LogInformation("Database is up to date.");
                return;
            }
            catch (Exception exception) when (attemptNumber < MaximumAttempts)
            {
                logger.LogWarning("Database not reachable yet (attempt {AttemptNumber}/{MaximumAttempts}): {Message}",
                    attemptNumber, MaximumAttempts, exception.Message);
                await Task.Delay(DelayBetweenAttempts);
            }
        }
    }
}

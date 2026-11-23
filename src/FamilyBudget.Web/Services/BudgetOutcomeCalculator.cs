using FamilyBudget.Web.Data;
using FamilyBudget.Web.Data.Budget;
using Microsoft.EntityFrameworkCore;

namespace FamilyBudget.Web.Services;

/// <summary>
/// One due date of one entry, with both the expected and the actually paid amount.
/// </summary>
/// <param name="EntryId">The entry (bill or income) this occurrence belongs to.</param>
/// <param name="EntryName">Name of the entry, e.g. "Elräkning".</param>
/// <param name="Kind">Expense or income.</param>
/// <param name="CategoryId">Category of the entry, or null if it has none.</param>
/// <param name="CategoryName">Category name, or null if it has none.</param>
/// <param name="DueDate">The date this occurrence falls due.</param>
/// <param name="ExpectedAmount">The amount the entry says it should be.</param>
/// <param name="PaidAmount">The amount registered as paid/received, or null if not paid yet.</param>
public sealed record OutcomeItem(
    int EntryId,
    string EntryName,
    EntryKind Kind,
    int? CategoryId,
    string? CategoryName,
    DateOnly DueDate,
    decimal ExpectedAmount,
    decimal? PaidAmount)
{
    /// <summary>
    /// The amount used in overviews and statistics: what was actually paid when it is paid,
    /// otherwise what is expected.
    /// </summary>
    public decimal EffectiveAmount => PaidAmount ?? ExpectedAmount;

    public bool IsPaid => PaidAmount is not null;
}

/// <summary>
/// Collects all due occurrences of a group's entries in a date range. Shared by the
/// year overview, the budget page and the statistics page so they all count the same way.
/// </summary>
public class BudgetOutcomeCalculator(IDbContextFactory<FamilyBudgetDbContext> contextFactory)
{
    /// <summary>All occurrences from <paramref name="rangeStart"/> to <paramref name="rangeEnd"/> (both inclusive).</summary>
    public async Task<List<OutcomeItem>> GetItemsAsync(int budgetGroupId, DateOnly rangeStart, DateOnly rangeEnd)
    {
        await using var databaseContext = await contextFactory.CreateDbContextAsync();

        // Only entries that can fall due in the range: started before it ends, not ended before it starts.
        var entries = await databaseContext.BudgetEntries
            .Where(entry => entry.BudgetGroupId == budgetGroupId
                && entry.FirstDueDate <= rangeEnd
                && (entry.EndDate == null || entry.EndDate >= rangeStart))
            .Include(entry => entry.Category)
            .Include(entry => entry.Payments.Where(payment => payment.DueDate >= rangeStart && payment.DueDate <= rangeEnd))
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();

        var outcomeItems = new List<OutcomeItem>();
        foreach (var entry in entries)
        {
            // Look up payments by due date so each occurrence finds its own payment quickly.
            var paymentsByDueDate = entry.Payments.ToDictionary(payment => payment.DueDate, payment => payment.Amount);

            foreach (var dueDate in DueDateCalculator.GetDueDates(entry, rangeStart, rangeEnd))
            {
                decimal? paidAmount = paymentsByDueDate.TryGetValue(dueDate, out var registeredAmount) ? registeredAmount : null;
                outcomeItems.Add(new OutcomeItem(
                    entry.Id, entry.Name, entry.Kind, entry.CategoryId, entry.Category?.Name,
                    dueDate, entry.Amount, paidAmount));
            }
        }
        return outcomeItems;
    }

    /// <summary>The first and last due date of any entry in the group, or null when the group has no entries.</summary>
    public async Task<(DateOnly FirstDate, DateOnly LastDate)?> GetDateSpanAsync(int budgetGroupId)
    {
        await using var databaseContext = await contextFactory.CreateDbContextAsync();
        var groupEntries = databaseContext.BudgetEntries.Where(entry => entry.BudgetGroupId == budgetGroupId);
        if (!await groupEntries.AnyAsync())
        {
            return null;
        }
        var firstDate = await groupEntries.MinAsync(entry => entry.FirstDueDate);
        // Recurring entries without end date go on forever; the caller decides how far ahead to look.
        var lastDate = await groupEntries.MaxAsync(entry => entry.FirstDueDate);
        return (firstDate, lastDate);
    }

    /// <summary>
    /// Every occurrence from the group's first entry to the end of the current year
    /// (or of the last one-off entry, if that is later). Empty when the group has no entries.
    /// </summary>
    /// <remarks>Recurring entries without end date go on forever, so the current year is the stop.</remarks>
    public async Task<(List<OutcomeItem> Items, int FirstYear, int LastYear)> GetAllItemsAsync(int budgetGroupId)
    {
        var currentYear = DateTime.Today.Year;
        var dateSpan = await GetDateSpanAsync(budgetGroupId);
        if (dateSpan is null)
        {
            return ([], currentYear, currentYear);
        }

        var firstYear = dateSpan.Value.FirstDate.Year;
        var lastYear = Math.Max(currentYear, dateSpan.Value.LastDate.Year);
        var items = await GetItemsAsync(budgetGroupId, new DateOnly(firstYear, 1, 1), new DateOnly(lastYear, 12, 31));
        return (items, firstYear, lastYear);
    }

    /// <summary>Monthly budget per category id for the group.</summary>
    public async Task<Dictionary<int, decimal>> GetMonthlyBudgetsAsync(int budgetGroupId)
    {
        await using var databaseContext = await contextFactory.CreateDbContextAsync();
        return await databaseContext.CategoryBudgets
            .Where(categoryBudget => categoryBudget.BudgetGroupId == budgetGroupId)
            .ToDictionaryAsync(categoryBudget => categoryBudget.CategoryId, categoryBudget => categoryBudget.MonthlyAmount);
    }
}

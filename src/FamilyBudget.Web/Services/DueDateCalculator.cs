using FamilyBudget.Web.Data.Budget;

namespace FamilyBudget.Web.Services;

/// <summary>
/// Works out on which dates an entry falls due, based on its first due date and periodicity.
/// </summary>
public static class DueDateCalculator
{
    /// <summary>Number of months between two due dates, or null for one-off entries.</summary>
    public static int? MonthsBetweenDueDates(Periodicity periodicity) => periodicity switch
    {
        Periodicity.Monthly => 1,
        Periodicity.Quarterly => 3,
        Periodicity.HalfYearly => 6,
        Periodicity.Yearly => 12,
        _ => null,
    };

    /// <summary>
    /// All due dates from <paramref name="rangeStart"/> to <paramref name="rangeEnd"/> (both inclusive).
    /// </summary>
    /// <remarks>
    /// Each date is calculated from the first due date (first.AddMonths(n)), not from the previous
    /// date, so an entry due on the 31st is due on the 31st again after a short month
    /// (Jan 31 → Feb 28 → Mar 31) instead of drifting to the 28th.
    /// </remarks>
    public static IEnumerable<DateOnly> GetDueDates(
        DateOnly firstDueDate, Periodicity periodicity, DateOnly? endDate, DateOnly rangeStart, DateOnly rangeEnd)
    {
        // Never look past the entry's own end date.
        var lastPossibleDate = endDate is { } entryEndDate && entryEndDate < rangeEnd ? entryEndDate : rangeEnd;

        var monthsBetween = MonthsBetweenDueDates(periodicity);
        if (monthsBetween is null)
        {
            // One-off entry.
            if (firstDueDate >= rangeStart && firstDueDate <= lastPossibleDate)
            {
                yield return firstDueDate;
            }
            yield break;
        }

        // Skip ahead to the first occurrence that could be inside the range, instead of looping from the start.
        var monthsFromFirstToRange = ((rangeStart.Year - firstDueDate.Year) * 12) + rangeStart.Month - firstDueDate.Month;
        var occurrenceNumber = Math.Max(0, (monthsFromFirstToRange / monthsBetween.Value) - 1);

        while (true)
        {
            var dueDate = firstDueDate.AddMonths(occurrenceNumber * monthsBetween.Value);
            if (dueDate > lastPossibleDate)
            {
                yield break;
            }
            if (dueDate >= rangeStart)
            {
                yield return dueDate;
            }
            occurrenceNumber++;
        }
    }

    /// <summary>Convenience overload for an entry.</summary>
    public static IEnumerable<DateOnly> GetDueDates(BudgetEntry entry, DateOnly rangeStart, DateOnly rangeEnd) =>
        GetDueDates(entry.FirstDueDate, entry.Periodicity, entry.EndDate, rangeStart, rangeEnd);

    /// <summary>The next due date on or after <paramref name="fromDate"/>, or null if the entry has ended.</summary>
    public static DateOnly? GetNextDueDate(BudgetEntry entry, DateOnly fromDate) =>
        GetDueDates(entry, fromDate, fromDate.AddYears(2)).Cast<DateOnly?>().FirstOrDefault();
}

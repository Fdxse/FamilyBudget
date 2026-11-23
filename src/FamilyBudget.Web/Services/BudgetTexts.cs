using System.Globalization;
using FamilyBudget.Web.Data.Budget;

namespace FamilyBudget.Web.Services;

/// <summary>Swedish display texts and formatting used across the budget pages.</summary>
public static class BudgetTexts
{
    private static readonly CultureInfo SwedishCulture = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>Formats an amount as Swedish kronor, e.g. "12 345,50 kr".</summary>
    public static string Money(decimal amount) => amount.ToString("#,##0.00 'kr'", SwedishCulture);

    /// <summary>Formats a date as "2026-11-25".</summary>
    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", SwedishCulture);

    /// <summary>Month name with capital first letter, e.g. "November 2026".</summary>
    public static string MonthTitle(int year, int month)
    {
        var monthName = SwedishCulture.DateTimeFormat.GetMonthName(month);
        return $"{char.ToUpper(monthName[0], SwedishCulture)}{monthName[1..]} {year}";
    }

    public static string Kind(EntryKind kind) => kind == EntryKind.Income ? "Inkomst" : "Utgift";

    public static string Periodicity(Periodicity periodicity) => periodicity switch
    {
        Data.Budget.Periodicity.Once => "Engång",
        Data.Budget.Periodicity.Monthly => "Varje månad",
        Data.Budget.Periodicity.Quarterly => "Varje kvartal",
        Data.Budget.Periodicity.HalfYearly => "Varje halvår",
        Data.Budget.Periodicity.Yearly => "Varje år",
        _ => periodicity.ToString(),
    };
}

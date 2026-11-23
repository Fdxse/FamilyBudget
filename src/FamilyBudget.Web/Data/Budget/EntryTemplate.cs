namespace FamilyBudget.Web.Data.Budget;

/// <summary>
/// A saved starting point for a new entry, e.g. "Lön" with the usual amount.
/// Choosing "Lägg till" on a template opens the new-entry form pre-filled with these values.
/// </summary>
public class EntryTemplate
{
    public int Id { get; set; }

    public int BudgetGroupId { get; set; }
    public BudgetGroup BudgetGroup { get; set; } = null!;

    public EntryKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public decimal Amount { get; set; }

    public Periodicity Periodicity { get; set; } = Periodicity.Once;

    /// <summary>Day of month the entry usually falls due (1–31); null means "today".</summary>
    public int? DayOfMonth { get; set; }

    public string? Note { get; set; }
}

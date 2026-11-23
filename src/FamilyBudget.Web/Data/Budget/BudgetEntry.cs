namespace FamilyBudget.Web.Data.Budget;

/// <summary>
/// A bill, larger expense or income that belongs to one budget group.
/// It falls due on <see cref="FirstDueDate"/> and then again according to
/// <see cref="Periodicity"/>, until <see cref="EndDate"/> (if set).
/// </summary>
public class BudgetEntry
{
    public int Id { get; set; }

    public int BudgetGroupId { get; set; }
    public BudgetGroup BudgetGroup { get; set; } = null!;

    public EntryKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Expected amount in SEK each time the entry falls due.</summary>
    public decimal Amount { get; set; }

    public Periodicity Periodicity { get; set; } = Periodicity.Monthly;

    public DateOnly FirstDueDate { get; set; }

    /// <summary>Last date the entry can fall due; null means it continues until further notice.</summary>
    public DateOnly? EndDate { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Due dates that have been marked as paid (or received, for income).</summary>
    public List<EntryPayment> Payments { get; set; } = [];
}

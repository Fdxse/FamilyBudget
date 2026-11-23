namespace FamilyBudget.Web.Data.Budget;

/// <summary>
/// Records that one specific due date of an entry has been paid (or, for income, received).
/// The actual amount can differ from the entry's expected amount, e.g. a salary with overtime.
/// </summary>
public class EntryPayment
{
    public int Id { get; set; }

    public int BudgetEntryId { get; set; }
    public BudgetEntry BudgetEntry { get; set; } = null!;

    /// <summary>The due date this payment settles.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Amount actually paid or received.</summary>
    public decimal Amount { get; set; }

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}

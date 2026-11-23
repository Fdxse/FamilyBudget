namespace FamilyBudget.Web.Data;

/// <summary>
/// A budget that one or more users share, e.g. "Fredrik och Anna" or "Kalle".
/// All bills, incomes and budgets (added later) belong to exactly one group,
/// which is how the family's budgets are kept separate.
/// </summary>
public class BudgetGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The users who can see and edit this group's budget.</summary>
    public List<BudgetGroupMembership> Memberships { get; set; } = [];
}

namespace FamilyBudget.Web.Data;

/// <summary>
/// Links a user to a budget group (many-to-many: a parent could for example
/// be a member of both the shared budget and a child's budget).
/// </summary>
public class BudgetGroupMembership
{
    public int BudgetGroupId { get; set; }
    public BudgetGroup BudgetGroup { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}

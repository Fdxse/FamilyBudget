namespace FamilyBudget.Web.Data.Budget;

/// <summary>
/// A category such as "Boende" or "Lön". Each budget group has its own list.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public int BudgetGroupId { get; set; }
    public BudgetGroup BudgetGroup { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
}

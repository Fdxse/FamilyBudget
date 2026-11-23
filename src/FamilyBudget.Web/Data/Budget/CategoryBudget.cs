namespace FamilyBudget.Web.Data.Budget;

/// <summary>
/// The planned amount per month for one category in one budget group,
/// e.g. "Mat och hushåll: 8 000 kr per månad". Used to compare plan against outcome.
/// </summary>
public class CategoryBudget
{
    public int Id { get; set; }

    public int BudgetGroupId { get; set; }
    public BudgetGroup BudgetGroup { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>Planned amount for one month.</summary>
    public decimal MonthlyAmount { get; set; }
}

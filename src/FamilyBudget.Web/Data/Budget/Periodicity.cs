namespace FamilyBudget.Web.Data.Budget;

/// <summary>How often an entry falls due.</summary>
public enum Periodicity
{
    /// <summary>Only once, on the first due date.</summary>
    Once = 0,
    Monthly = 1,
    Quarterly = 2,
    HalfYearly = 3,
    Yearly = 4,
}

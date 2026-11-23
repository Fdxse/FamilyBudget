using Microsoft.AspNetCore.Identity;

namespace FamilyBudget.Web.Data;

/// <summary>
/// A person who can log in. Extends the standard ASP.NET Core Identity user
/// with a friendly display name and the budget groups the person belongs to.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Name shown in the UI, e.g. "Fredrik".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The budget groups this user is a member of.</summary>
    public List<BudgetGroupMembership> BudgetGroupMemberships { get; set; } = [];
}

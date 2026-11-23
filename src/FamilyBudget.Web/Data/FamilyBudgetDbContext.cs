using Microsoft.EntityFrameworkCore;

namespace FamilyBudget.Web.Data;

/// <summary>
/// Entity Framework Core context for the FamilyBudget database.
/// Tables (users, budget groups, bills, incomes ...) are added here in later steps.
/// </summary>
public class FamilyBudgetDbContext(DbContextOptions<FamilyBudgetDbContext> options) : DbContext(options)
{
}

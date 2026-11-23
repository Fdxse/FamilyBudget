using System.Security.Claims;
using FamilyBudget.Web.Data;
using FamilyBudget.Web.Data.Budget;
using Microsoft.EntityFrameworkCore;

namespace FamilyBudget.Web.Services;

/// <summary>
/// Central place for "may this user see this budget group?". Every budget page goes through
/// here, so a user can never open another group's data by changing the number in the address.
/// </summary>
public class BudgetGroupAccess(IDbContextFactory<FamilyBudgetDbContext> contextFactory)
{
    /// <summary>Categories created for a group the first time someone opens it.</summary>
    private static readonly string[] DefaultCategoryNames =
    [
        "Boende", "El och värme", "Försäkringar", "Lån", "Bil och transport", "Abonnemang",
        "Barn", "Mat och hushåll", "Sparande", "Övrigt",
        "Lön", "Barnbidrag", "Övriga inkomster",
    ];

    /// <summary>The logged-in user's id, read from the login cookie's claims.</summary>
    public static string? GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Returns the group if <paramref name="user"/> is a member of it, otherwise null.
    /// </summary>
    public async Task<BudgetGroup?> GetGroupForMemberAsync(ClaimsPrincipal user, int budgetGroupId)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return null;
        }

        await using var databaseContext = await contextFactory.CreateDbContextAsync();
        var budgetGroup = await databaseContext.BudgetGroups
            .FirstOrDefaultAsync(group => group.Id == budgetGroupId
                && group.Memberships.Any(membership => membership.UserId == userId));

        if (budgetGroup is not null && !budgetGroup.DefaultCategoriesCreated)
        {
            await CreateDefaultCategoriesAsync(databaseContext, budgetGroup);
        }

        return budgetGroup;
    }

    /// <summary>All groups the user is a member of, sorted by name.</summary>
    public async Task<List<BudgetGroup>> GetGroupsForMemberAsync(ClaimsPrincipal user)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return [];
        }

        await using var databaseContext = await contextFactory.CreateDbContextAsync();
        return await databaseContext.BudgetGroups
            .Where(group => group.Memberships.Any(membership => membership.UserId == userId))
            .OrderBy(group => group.Name)
            .ToListAsync();
    }

    private static async Task CreateDefaultCategoriesAsync(FamilyBudgetDbContext databaseContext, BudgetGroup budgetGroup)
    {
        var existingNames = await databaseContext.Categories
            .Where(category => category.BudgetGroupId == budgetGroup.Id)
            .Select(category => category.Name)
            .ToListAsync();

        foreach (var categoryName in DefaultCategoryNames.Except(existingNames))
        {
            databaseContext.Categories.Add(new Category { BudgetGroupId = budgetGroup.Id, Name = categoryName });
        }

        budgetGroup.DefaultCategoriesCreated = true;
        await databaseContext.SaveChangesAsync();
    }
}

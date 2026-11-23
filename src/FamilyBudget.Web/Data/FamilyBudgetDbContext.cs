using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyBudget.Web.Data;

/// <summary>
/// Entity Framework Core context for the FamilyBudget database.
/// Inherits the ASP.NET Core Identity tables (users, roles, logins ...)
/// and adds the application's own tables.
/// </summary>
public class FamilyBudgetDbContext(DbContextOptions<FamilyBudgetDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<BudgetGroup> BudgetGroups => Set<BudgetGroup>();
    public DbSet<BudgetGroupMembership> BudgetGroupMemberships => Set<BudgetGroupMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Let Identity configure its own tables first.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(user =>
        {
            user.Property(applicationUser => applicationUser.DisplayName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<BudgetGroup>(budgetGroup =>
        {
            budgetGroup.Property(group => group.Name).HasMaxLength(100).IsRequired();
            budgetGroup.HasIndex(group => group.Name).IsUnique();
        });

        modelBuilder.Entity<BudgetGroupMembership>(membership =>
        {
            // A user can be a member of a given group only once.
            membership.HasKey(link => new { link.BudgetGroupId, link.UserId });

            membership.HasOne(link => link.BudgetGroup)
                .WithMany(group => group.Memberships)
                .HasForeignKey(link => link.BudgetGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            membership.HasOne(link => link.User)
                .WithMany(user => user.BudgetGroupMemberships)
                .HasForeignKey(link => link.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

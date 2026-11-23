using FamilyBudget.Web.Data.Budget;
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
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
    public DbSet<EntryPayment> EntryPayments => Set<EntryPayment>();
    public DbSet<EntryTemplate> EntryTemplates => Set<EntryTemplate>();

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

        modelBuilder.Entity<Category>(category =>
        {
            category.Property(item => item.Name).HasMaxLength(100).IsRequired();
            // Category names are unique within a group.
            category.HasIndex(item => new { item.BudgetGroupId, item.Name }).IsUnique();
            category.HasOne(item => item.BudgetGroup).WithMany().HasForeignKey(item => item.BudgetGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetEntry>(entry =>
        {
            entry.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entry.Property(item => item.Amount).HasPrecision(12, 2);
            entry.Property(item => item.Note).HasMaxLength(1000);
            entry.HasIndex(item => item.BudgetGroupId);
            entry.HasOne(item => item.BudgetGroup).WithMany().HasForeignKey(item => item.BudgetGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            // Deleting a category keeps the entries, just without category.
            entry.HasOne(item => item.Category).WithMany().HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EntryPayment>(payment =>
        {
            payment.Property(item => item.Amount).HasPrecision(12, 2);
            // One payment per entry and due date.
            payment.HasIndex(item => new { item.BudgetEntryId, item.DueDate }).IsUnique();
            payment.HasOne(item => item.BudgetEntry).WithMany(entry => entry.Payments)
                .HasForeignKey(item => item.BudgetEntryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EntryTemplate>(template =>
        {
            template.Property(item => item.Name).HasMaxLength(150).IsRequired();
            template.Property(item => item.Amount).HasPrecision(12, 2);
            template.Property(item => item.Note).HasMaxLength(1000);
            template.HasOne(item => item.BudgetGroup).WithMany().HasForeignKey(item => item.BudgetGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            template.HasOne(item => item.Category).WithMany().HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
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

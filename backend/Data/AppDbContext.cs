using Microsoft.EntityFrameworkCore;
using Omnom.Api.Data.Entities;

namespace Omnom.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<MealItem> MealItems => Set<MealItem>();
    public DbSet<FoodReference> FoodReferences => Set<FoodReference>();
    public DbSet<DailyTarget> DailyTargets => Set<DailyTarget>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MealEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Date);
            entity.HasMany(e => e.Items)
                  .WithOne(i => i.MealEntry)
                  .HasForeignKey(i => i.MealEntryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MealItem>(entity =>
        {
            entity.HasKey(i => i.Id);
        });

        modelBuilder.Entity<FoodReference>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => f.NormalizedQuery);
            entity.HasIndex(f => f.FdcId);
        });

        modelBuilder.Entity<DailyTarget>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.EffectiveDate);
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
        });
    }
}

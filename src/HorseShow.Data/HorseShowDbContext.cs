using HorseShow.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Data;

public class HorseShowDbContext(DbContextOptions<HorseShowDbContext> options) : DbContext(options)
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Show> Shows => Set<Show>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<ShowClass> ShowClasses => Set<ShowClass>();
    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<RiderAlias> RiderAliases => Set<RiderAlias>();
    public DbSet<Horse> Horses => Set<Horse>();
    public DbSet<HorseAlias> HorseAliases => Set<HorseAlias>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<PointSchedule> PointSchedules => Set<PointSchedule>();
    public DbSet<PointScheduleRow> PointScheduleRows => Set<PointScheduleRow>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<MappingProfile> MappingProfiles => Set<MappingProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HorseShowDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Result>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}

using HorseShow.Core.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseShow.Data.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Organization).HasMaxLength(200);
        b.HasIndex(x => x.Year);

        b.HasOne(x => x.DefaultPointSchedule)
            .WithMany()
            .HasForeignKey(x => x.DefaultPointScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Shows).WithOne(x => x.Season).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Divisions).WithOne(x => x.Season).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShowConfiguration : IEntityTypeConfiguration<Show>
{
    public void Configure(EntityTypeBuilder<Show> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Venue).HasMaxLength(200);
        b.HasIndex(x => new { x.SeasonId, x.StartDate });
        b.HasMany(x => x.Classes).WithOne(x => x.Show).HasForeignKey(x => x.ShowId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.SeasonId, x.Name }).IsUnique();
        b.HasOne(x => x.PointSchedule).WithMany().HasForeignKey(x => x.PointScheduleId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ShowClassConfiguration : IEntityTypeConfiguration<ShowClass>
{
    public void Configure(EntityTypeBuilder<ShowClass> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ClassNumber).HasMaxLength(20);
        b.Property(x => x.JudgeName).HasMaxLength(200);
        b.HasIndex(x => new { x.ShowId, x.SortOrder });
        b.HasOne(x => x.Division).WithMany().HasForeignKey(x => x.DivisionId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Results).WithOne(x => x.ShowClass).HasForeignKey(x => x.ShowClassId).OnDelete(DeleteBehavior.Cascade);
    }
}

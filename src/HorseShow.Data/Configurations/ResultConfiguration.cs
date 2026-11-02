using HorseShow.Core.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseShow.Data.Configurations;

public class ResultConfiguration : IEntityTypeConfiguration<Result>
{
    public void Configure(EntityTypeBuilder<Result> b)
    {
        b.Property(x => x.BackNumber).HasMaxLength(20);
        b.Property(x => x.ScoreNote).HasMaxLength(200);
        b.Property(x => x.Score).HasPrecision(9, 3);
        b.Property(x => x.Points).HasPrecision(9, 3);

        b.HasIndex(x => x.RiderId);
        b.HasIndex(x => x.HorseId);
        b.HasIndex(x => x.ImportBatchId);

        b.HasOne(x => x.Rider).WithMany().HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Horse).WithMany().HasForeignKey(x => x.HorseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.SetNull);

        b.ToTable(t => t.HasCheckConstraint("CK_Result_RiderOrHorse", "\"RiderId\" IS NOT NULL OR \"HorseId\" IS NOT NULL"));
    }
}

public class PointScheduleConfiguration : IEntityTypeConfiguration<PointSchedule>
{
    public void Configure(EntityTypeBuilder<PointSchedule> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Rows).WithOne(x => x.PointSchedule).HasForeignKey(x => x.PointScheduleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointScheduleRowConfiguration : IEntityTypeConfiguration<PointScheduleRow>
{
    public void Configure(EntityTypeBuilder<PointScheduleRow> b)
    {
        b.Property(x => x.Points).HasPrecision(9, 3);
        b.HasIndex(x => new { x.PointScheduleId, x.Placing, x.MinEntries }).IsUnique();
    }
}

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.RawContentHash).HasMaxLength(64);
        b.HasIndex(x => x.RawContentHash);
        b.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Show).WithMany().HasForeignKey(x => x.ShowId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.MappingProfile).WithMany().HasForeignKey(x => x.MappingProfileId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MappingProfileConfiguration : IEntityTypeConfiguration<MappingProfile>
{
    public void Configure(EntityTypeBuilder<MappingProfile> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.SourceHint).HasMaxLength(200);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

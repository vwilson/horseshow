using HorseShow.Core.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseShow.Data.Configurations;

public class RiderConfiguration : IEntityTypeConfiguration<Rider>
{
    public void Configure(EntityTypeBuilder<Rider> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.Barn).HasMaxLength(200);
        b.Property(x => x.Group).HasMaxLength(100);
        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.Group);
        b.HasMany(x => x.Aliases).WithOne(x => x.Rider).HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RiderAliasConfiguration : IEntityTypeConfiguration<RiderAlias>
{
    public void Configure(EntityTypeBuilder<RiderAlias> b)
    {
        b.Property(x => x.Alias).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Alias).IsUnique();
    }
}

public class HorseConfiguration : IEntityTypeConfiguration<Horse>
{
    public void Configure(EntityTypeBuilder<Horse> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Owner).HasMaxLength(200);
        b.Property(x => x.RegistrationNumber).HasMaxLength(50);
        b.HasIndex(x => x.Name);
        b.HasMany(x => x.Aliases).WithOne(x => x.Horse).HasForeignKey(x => x.HorseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HorseAliasConfiguration : IEntityTypeConfiguration<HorseAlias>
{
    public void Configure(EntityTypeBuilder<HorseAlias> b)
    {
        b.Property(x => x.Alias).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Alias).IsUnique();
    }
}

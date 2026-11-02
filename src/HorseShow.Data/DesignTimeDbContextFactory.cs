using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HorseShow.Data;

/// <summary>Used only by `dotnet ef migrations add` so the tooling does not need a host project.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HorseShowDbContext>
{
    public HorseShowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HorseShowDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new HorseShowDbContext(options);
    }
}

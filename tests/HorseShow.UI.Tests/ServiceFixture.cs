using HorseShow.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HorseShow.UI.Tests;

/// <summary>A migrated temp database plus the view services wired as the hosts wire them.</summary>
public sealed class ServiceFixture : IAsyncDisposable
{
    public TempDatabase Db { get; } = new();
    public ActiveSeasonService Active { get; }
    public SeasonsViewService Seasons { get; }
    public ShowsViewService Shows { get; }
    public ParticipantsViewService Participants { get; }
    public DashboardViewService Dashboard { get; }

    private ServiceFixture()
    {
        Active = new ActiveSeasonService(Db.Factory);
        Seasons = new SeasonsViewService(Db.Factory, Active);
        Shows = new ShowsViewService(Db.Factory);
        Participants = new ParticipantsViewService(Db.Factory);
        Dashboard = new DashboardViewService(Db.Factory, Active);
    }

    public static async Task<ServiceFixture> CreateAsync()
    {
        var f = new ServiceFixture();
        await f.Db.Initializer.InitializeAsync();
        return f;
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

using HorseShow.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HorseShow.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MudBlazor and the UI view services. Hosts must also call
    /// <c>AddHorseShowData</c> and register the <see cref="Platform"/> interfaces.
    /// </summary>
    public static IServiceCollection AddHorseShowUi(this IServiceCollection services)
    {
        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 4000;
        });
        services.AddSingleton<StartupGate>();
        services.AddScoped<ThemeService>();
        services.AddScoped<ActiveSeasonService>();
        services.AddScoped<DashboardViewService>();
        services.AddScoped<SeasonsViewService>();
        services.AddScoped<ShowsViewService>();
        services.AddScoped<ParticipantsViewService>();
        services.AddScoped<StandingsViewService>();
        services.AddScoped<SchedulesViewService>();
        services.AddScoped<ImportViewService>();
        services.AddScoped<ImportSession>();
        services.AddScoped<MaintenanceViewService>();
        return services;
    }
}

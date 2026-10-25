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
        services.AddMudServices();
        services.AddSingleton<StartupGate>();
        services.AddScoped<ThemeService>();
        services.AddScoped<DashboardViewService>();
        return services;
    }
}

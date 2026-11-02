using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HorseShow.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a pooled <see cref="IDbContextFactory{HorseShowDbContext}"/>. Blazor components
    /// must create a short-lived context per operation rather than injecting a scoped context,
    /// because in Blazor Hybrid a scope lives as long as the window.
    /// </summary>
    public static IServiceCollection AddHorseShowData(this IServiceCollection services, DatabaseOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<SqlitePragmaInterceptor>();
        services.AddDbContextFactory<HorseShowDbContext>((sp, o) =>
        {
            o.UseSqlite(options.ConnectionString);
            o.AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>());
        });
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }
}

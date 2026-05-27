using Microsoft.Extensions.DependencyInjection;
using ResetScore.Configuration;
using ResetScore.Modules;

namespace ResetScore;

internal static class ModuleDependencyInjection
{
    internal static IServiceCollection AddModules(this IServiceCollection services)
    {
        // Config (constructs ConVars on instantiation — not an IModule)
        services.AddSingleton<IResetScoreConfig, ResetScoreConfig>();

        // Core reset logic + commands
        services.AddSingleton<ResetScoreModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<ResetScoreModule>());

        return services;
    }
}

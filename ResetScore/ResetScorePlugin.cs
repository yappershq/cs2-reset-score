using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace ResetScore;

/// <summary>
/// Reset Score — lets players reset their own scoreboard stats (kills/deaths/assists/damage/
/// score/MVPs) with a chat command, plus an admin command to set another player's stats.
///
/// ModSharp port of the CounterStrikeSharp plugin stefanx111/cs2-SimpleResetScore
/// (https://github.com/stefanx111/cs2-SimpleResetScore). Independent reimplementation; no
/// original code is shipped.
/// </summary>
public sealed class ResetScorePlugin : IModSharpModule
{
    public string DisplayName   => "ResetScore";
    public string DisplayAuthor => "yappershq — ported from StefanX (cs2-SimpleResetScore, CounterStrikeSharp)";

    private readonly ILogger<ResetScorePlugin> _logger;
    private readonly ServiceProvider           _serviceProvider;

    public ResetScorePlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);

        var loggerFactory = sharedSystem.GetLoggerFactory();
        _logger = loggerFactory.CreateLogger<ResetScorePlugin>();

        _ = new InterfaceBridge(dllPath, sharpPath, sharedSystem, loggerFactory);

        var services = new ServiceCollection();
        services.AddSingleton(sharedSystem);
        services.AddSingleton(InterfaceBridge.Instance);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(LoggerFactoryLogger<>));

        services.AddModules();

        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.Init(), "Init");

        return true;
    }

    public void PostInit()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.OnPostInit(), "PostInit");
    }

    public void OnAllModulesLoaded()
    {
        // Resolve optional modules in OAM — ModSharp guarantees all PostInits finish before any
        // OAM fires, so other plugins' published interfaces (Localizer, AdminManager) are live.
        InterfaceBridge.Instance.InitLocalizer();
        InterfaceBridge.Instance.InitAdminManager();

        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.OnAllSharpModulesLoaded(), "OnAllModulesLoaded");

        _logger.LogInformation("[ResetScore] Plugin loaded successfully");
    }

    public void Shutdown()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.Shutdown(), "Shutdown");

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    public void OnLibraryConnected(string name)
    {
        if (name == "LocalizerManager")
            InterfaceBridge.Instance.InitLocalizer();
        else if (name == "AdminManager")
            InterfaceBridge.Instance.InitAdminManager();
    }

    public void OnLibraryDisconnect(string name) { }

    private void CallSafe(IModule module, Action<IModule> action, string phase)
    {
        try
        {
            action(module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ResetScore] Error in {Phase} for {Module}", phase, module.GetType().Name);
        }
    }
}

internal sealed class LoggerFactoryLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _inner = factory.CreateLogger(typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}

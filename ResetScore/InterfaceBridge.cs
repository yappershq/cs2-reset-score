using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace ResetScore;

internal sealed class InterfaceBridge
{
    internal static InterfaceBridge Instance { get; private set; } = null!;

    // === Paths ===
    internal string SharpPath { get; }
    internal string DllPath   { get; }

    // === Managers ===
    internal IConVarManager      ConVarManager      { get; }
    internal IClientManager      ClientManager      { get; }
    internal IModSharp           ModSharp           { get; }
    internal ILoggerFactory      LoggerFactory      { get; }
    internal ISharpModuleManager SharpModuleManager { get; }

    // === Optional modules (resolved in OnAllModulesLoaded) ===
    internal ILocalizerManager? LocalizerManager { get; private set; }
    internal IAdminManager?     AdminManager     { get; private set; }

    public InterfaceBridge(
        string         dllPath,
        string         sharpPath,
        ISharedSystem  sharedSystem,
        ILoggerFactory loggerFactory)
    {
        Instance = this;

        SharpPath = sharpPath;
        DllPath   = dllPath;

        ConVarManager      = sharedSystem.GetConVarManager();
        ClientManager      = sharedSystem.GetClientManager();
        ModSharp           = sharedSystem.GetModSharp();
        LoggerFactory      = loggerFactory;
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
    }

    internal void InitLocalizer()
    {
        var iface = SharpModuleManager
            .GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity);
        if (iface?.Instance is not { } lm)
            return;

        LocalizerManager = lm;
        lm.LoadLocaleFile("resetscore", suppressDuplicationWarnings: true);
    }

    internal void InitAdminManager()
    {
        var iface = SharpModuleManager
            .GetOptionalSharpModuleInterface<IAdminManager>(IAdminManager.Identity);
        if (iface?.Instance is not { } am)
            return;

        AdminManager = am;
        RegisterAdminPermissions();
    }

    private void RegisterAdminPermissions()
    {
        if (AdminManager is not { } am)
            return;

        am.MountAdminManifest("ResetScore", static () => new AdminTableManifest(
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["resetscore"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "@resetscore/vip",
                    "@resetscore/admin",
                },
            },
            [],
            []));
    }

    /// <summary>
    /// Localize a string for a specific client. Falls back to <paramref name="key"/> if the
    /// localizer is unavailable or the key is missing.
    /// </summary>
    internal string LocalizeFor(IGameClient client, string key, params object?[] args)
    {
        var lm = LocalizerManager;
        if (lm is null)
            return key;

        try
        {
            return lm.For(client).Text(key, args);
        }
        catch (Exception)
        {
            return key;
        }
    }
}

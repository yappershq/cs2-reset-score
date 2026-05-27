using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ResetScore.Configuration;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace ResetScore.Modules;

/// <summary>
/// Handles the self-reset (<c>!rs</c>) and admin set-score (<c>!setscore</c>) commands. Zeroes a
/// player's scoreboard stats (kills/deaths/assists/damage/score/MVPs) and flushes the relevant
/// networked fields so every client's scoreboard refreshes.
/// </summary>
internal sealed class ResetScoreModule : IModule, IClientListener
{
    private readonly InterfaceBridge               _bridge;
    private readonly IResetScoreConfig             _config;
    private readonly ILogger<ResetScoreModule>     _logger;

    // Per-slot cooldown tracking (engine time of last self-reset).
    private readonly double[] _lastResetTime = new double[64];

    private readonly IClientManager.DelegateClientCommand _resetCallback;
    private readonly IClientManager.DelegateClientCommand _setScoreCallback;

    public ResetScoreModule(InterfaceBridge bridge, IResetScoreConfig config)
    {
        _bridge = bridge;
        _config = config;
        _logger = bridge.LoggerFactory.CreateLogger<ResetScoreModule>();

        _resetCallback    = OnResetScoreCommand;
        _setScoreCallback = OnSetScoreCommand;
    }

    public bool Init()
    {
        _bridge.ClientManager.InstallClientListener(this);
        _bridge.ClientManager.InstallCommandCallback("rs",         _resetCallback);
        _bridge.ClientManager.InstallCommandCallback("resetscore", _resetCallback);
        _bridge.ClientManager.InstallCommandCallback("setscore",   _setScoreCallback);

        _logger.LogInformation("[ResetScore] Initialized");
        return true;
    }

    public void Shutdown()
    {
        _bridge.ClientManager.RemoveClientListener(this);
        _bridge.ClientManager.RemoveCommandCallback("rs",         _resetCallback);
        _bridge.ClientManager.RemoveCommandCallback("resetscore", _resetCallback);
        _bridge.ClientManager.RemoveCommandCallback("setscore",   _setScoreCallback);

        Array.Clear(_lastResetTime);
    }

    // ===== IClientListener =====

    int IClientListener.ListenerPriority => 0;
    int IClientListener.ListenerVersion  => IClientListener.ApiVersion;

    public void OnClientPutInServer(IGameClient client)
        => _lastResetTime[client.Slot] = 0d;

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
        => _lastResetTime[client.Slot] = 0d;

    // ===== Commands =====

    private ECommandAction OnResetScoreCommand(IGameClient client, StringCommand command)
    {
        if (!client.IsInGame || client.IsFakeClient)
            return ECommandAction.Skipped;

        if (!_config.Enabled)
            return ECommandAction.Handled;

        if (_config.OnlyVip && !HasPermission(client, _config.VipPermission))
        {
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.only_vip"));
            return ECommandAction.Handled;
        }

        // Check (but don't yet consume) the cooldown — only consume it on a successful reset.
        var now = 0.0;
        if (_config.Cooldown > 0)
        {
            now = _bridge.ModSharp.GetGlobals().CurTime;
            var last = _lastResetTime[client.Slot];
            var remaining = _config.Cooldown - (now - last);
            if (last > 0 && remaining > 0)
            {
                client.Print(HudPrintChannel.Chat,
                    _bridge.LocalizeFor(client, "resetscore.cooldown", (int)Math.Ceiling(remaining)));
                return ECommandAction.Handled;
            }
        }

        var controller = client.GetPlayerController();
        if (controller is not { IsValidEntity: true })
            return ECommandAction.Handled;

        if (!SetStats(controller, 0, 0, 0, 0, 0, 0))
        {
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.unavailable"));
            return ECommandAction.Handled;
        }

        if (_config.Cooldown > 0)
            _lastResetTime[client.Slot] = now;

        client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.reset"));
        return ECommandAction.Handled;
    }

    private ECommandAction OnSetScoreCommand(IGameClient client, StringCommand command)
    {
        if (!client.IsInGame || client.IsFakeClient)
            return ECommandAction.Skipped;

        if (!HasPermission(client, _config.AdminPermission))
        {
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.only_admin"));
            return ECommandAction.Handled;
        }

        // ArgCount excludes the command name; GetArg is 1-indexed.
        // Usage: setscore <target> <kills> <deaths> <assists> <damage> <mvps> <score>
        if (command.ArgCount < 7)
        {
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.setscore_usage"));
            return ECommandAction.Handled;
        }

        var targetName = command.GetArg(1);
        if (!TryParseInt(command.GetArg(2), out var kills)   ||
            !TryParseInt(command.GetArg(3), out var deaths)  ||
            !TryParseInt(command.GetArg(4), out var assists) ||
            !TryParseInt(command.GetArg(5), out var damage)  ||
            !TryParseInt(command.GetArg(6), out var mvps)    ||
            !TryParseInt(command.GetArg(7), out var score))
        {
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.setscore_usage"));
            return ECommandAction.Handled;
        }

        var matched = 0;
        foreach (var target in _bridge.ClientManager.GetGameClients(true))
        {
            if (!target.IsInGame || target.IsFakeClient || target.IsHltv)
                continue;

            var controller = target.GetPlayerController();
            if (controller is not { IsValidEntity: true })
                continue;

            if (controller.PlayerName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!SetStats(controller, kills, deaths, assists, damage, mvps, score))
                continue;

            client.Print(HudPrintChannel.Chat,
                _bridge.LocalizeFor(client, "resetscore.setscore_done", controller.PlayerName));
            matched++;
        }

        if (matched == 0)
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.no_target", targetName));
        else if (matched > 1)
            client.Print(HudPrintChannel.Chat, _bridge.LocalizeFor(client, "resetscore.setscore_multiple", matched));

        return ECommandAction.Handled;
    }

    // ===== Reset logic =====

    /// <summary>
    /// Applies the given stats to the controller and flushes the networked fields. Returns false
    /// without writing anything if the action-tracking service isn't available yet, so we never
    /// leave a half-reset scoreboard (score zeroed but K/D intact).
    /// </summary>
    private bool SetStats(IPlayerController controller, int kills, int deaths, int assists, int damage, int mvps, int score)
    {
        var stats = controller.GetActionTrackingService()?.GetMatchStats();
        if (stats is null)
        {
            _logger.LogWarning("[ResetScore] ActionTrackingService unavailable for {Name}; skipping reset",
                controller.PlayerName);
            return false;
        }

        stats.Kills   = kills;
        stats.Deaths  = deaths;
        stats.Assists = assists;
        stats.Damage  = damage;

        controller.Score = score;
        if (!_config.KeepMvp)
            controller.MvpCount = mvps;

        // Flush the networked fields so every client's scoreboard re-reads the new values.
        // m_pActionTrackingServices covers kills/deaths/assists/damage in one go.
        controller.NetworkStateChanged("m_pActionTrackingServices");
        controller.NetworkStateChanged("m_iScore");
        if (!_config.KeepMvp)
            controller.NetworkStateChanged("m_iMVPs");

        return true;
    }

    // ===== Helpers =====

    private bool HasPermission(IGameClient client, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return true;

        var am = _bridge.AdminManager;
        if (am is null)
            return false; // can't verify a gated permission without AdminManager → deny

        var admin = am.GetAdmin(client.SteamId);
        return admin is not null && admin.HasPermission(permission);
    }

    private static bool TryParseInt(string raw, out int value)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
